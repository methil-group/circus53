/// <summary>
/// Fichier : DitherPass.cs — Implémentation de l'effet de dithering par palette.
/// 
/// <see cref="MethilDitherPass"/> est la classe centrale de l'effet MethilDither.
/// Elle hérite de <see cref="VolFx.Pass"/> et implémente :
/// 
/// <list type="bullet">
///   <item><b>Génération de LUTs</b> — Pour chaque palette, génère trois textures 3D LUT :
///     <list type="bullet">
///       <item>LUT Palette — la couleur de remplacement la plus proche</item>
///       <item>LUT Quant — la deuxième couleur la plus proche (pour le dithering)</item>
///       <item>LUT Measure — la distance à la couleur la plus proche (erreur de quantification)</item>
///     </list>
///   </item>
///   <item><b>Cache de palettes</b> — Les LUTs sont mises en cache par texture de palette
///       pour éviter de les regénérer à chaque frame.</item>
///   <item><b>Deux modes de rendu</b> :
///     <list type="bullet">
///       <item><b>Dither</b> — Motif de dithering ordonné avec gigue temporelle</item>
///       <item><b>Noise</b> — Bruit blanc généré dynamiquement</item>
///     </list>
///   </item>
///   <item><b>Sécurité WebGL</b> — Réduction de la résolution de la texture de bruit (max 512px)
///       et blocs try/catch autour des allocations pour éviter les crashes.</item>
/// </list>
/// 
/// Cycle de vie :
/// <list type="number">
///   <item><see cref="Init"/> — Reset des compteurs de frame et du cache de palettes.</item>
///   <item><see cref="Validate"/> — Appelé chaque frame : lit le VolumeComponent,
///       génère/cache les LUTs, configure les propriétés du material.</item>
/// </list>
/// </summary>
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

namespace VolFx
{
    /// <summary>
    /// Passe de dithering par palette.
    /// Le shader est localisé automatiquement via l'attribut <see cref="ShaderNameAttribute"/>.
    /// </summary>
    [ShaderName("Hidden/VolFx/MethilDither")]
    public class MethilDitherPass : VolFx.Pass
    {
        // =======================================================================
        // IDs des propriétés shader (calculés une fois, réutilisés chaque frame)
        // =======================================================================
        private static readonly int s_Weight      = Shader.PropertyToID("_Weight");
        private static readonly int s_PaletteTex  = Shader.PropertyToID("_PaletteTex");
        private static readonly int s_QuantTex    = Shader.PropertyToID("_QuantTex");
        private static readonly int s_MeasureTex  = Shader.PropertyToID("_MeasureTex");
        private static readonly int s_DitherTex   = Shader.PropertyToID("_MethilDitherTex");
        private static readonly int s_Dither      = Shader.PropertyToID("_MethilDither");
        private static readonly int s_PatternData = Shader.PropertyToID("_PatternData");
        private static readonly int s_DitherMad   = Shader.PropertyToID("_MethilDitherMad");
        
        // =======================================================================
        // Paramètres configurables dans l'inspecteur (valeurs par défaut)
        // =======================================================================
        
        /// <summary>Échelle du bruit d'écran en mode Noise (0-1).</summary>
        [Range(0, 1)]
        [Tooltip("Screen nose scale in NoseMode")]
        public float     _noiseScale = .5f;
        
        /// <summary>Plage de tuilage du motif de dithering, mappée depuis la valeur Scale.</summary>
        [Tooltip("Dithering pattern tiling range mapped from Scale value")]
        public Vector2Int _scaleRange = new Vector2Int(1, 100);
        
        [Header("Default volume overrides")]
        /// <summary>Palette par défaut (utilisée si aucun Volume ne la surcharge).</summary>
        [Tooltip("Default palette")]
        public Texture2D _palette;
        /// <summary>Motif de dithering par défaut.</summary>
        [Tooltip("Default pattern dithering pattern")]
        public Texture2D _pattern;
        /// <summary>Mode par défaut : Dither (motif) ou Noise (bruit).</summary>
        [Tooltip("Default screen noise mode")]
        public Mode      _noiseMode = Mode.Noise;
        /// <summary>État de pixellisation par défaut.</summary>
        [Tooltip("Default pixelate state if not set in volume")]
        public bool      _pixelate = true;
        /// <summary>Échelle d'image par défaut (1 = pas de pixelation).</summary>
        [Range(0, 1)]
        [Tooltip("Default image scale")]
        public float     _scale = .735f;
        /// <summary>FPS par défaut pour la gigue du dithering (0 = chaque frame).</summary>
        [Tooltip("Default frame rate, dithering jitter")]
        [Range(0, 120)]
        public int       _frameRate;
        
        // =======================================================================
        // État interne
        // =======================================================================
        
        /// <summary>Taille de la LUT (fixée à x16). Les tailles x32 et x64 sont commentées.</summary>
        private LutGenerator.LutSize _lutSize = LutGenerator.LutSize.x16;
        /// <summary>Courbe gamma utilisée pour la comparaison des couleurs (rec601 par défaut).</summary>
        private LutGenerator.Gamma   _gamma   = LutGenerator.Gamma.rec601;
        
        /// <summary>Compteur de frame pour la gigue temporelle.</summary>
        private int                                _frame;
        /// <summary>Cache de LUTs par texture de palette.</summary>
        private Dictionary<Texture2D, PaletteCash> _paletteCash = new Dictionary<Texture2D, PaletteCash>();
        
        /// <summary>Texture de bruit générée dynamiquement pour le mode Noise.</summary>
        private Texture2D            _noiseTex;
        /// <summary>Vecteur de transformation pour le dithering (scale.xy + jitter.zw).</summary>
        private Vector4              _ditherMad;
        /// <summary>Mode précédent (pour détecter les changements et mettre à jour les keywords).</summary>
        private Mode                 _noiseModePrev;
        /// <summary>Taille de LUT précédente (non utilisé, tailles multiples commentées).</summary>
        private LutGenerator.LutSize _lutSizePrev;

        /// <summary>
        /// Cache de palette : contient les trois textures LUT générées.
        /// </summary>
        public class PaletteCash
        {
            /// <summary>LUT de palette (couleur de remplacement la plus proche).</summary>
            public Texture2D _palette;
            /// <summary>LUT de quantification (deuxième couleur la plus proche).</summary>
            public Texture2D _quant;
            /// <summary>LUT de mesure (distance/erreur).</summary>
            public Texture2D _measure;
        }

        /// <summary>
        /// Mode de dithering.
        /// </summary>
        public enum Mode
        {
            /// <summary>Dithering ordonné par motif.</summary>
            Dither,
            /// <summary>Dithering par bruit aléatoire.</summary>
            Noise
        }

        // =======================================================================
        // INITIALISATION
        // =======================================================================
        
        /// <summary>
        /// Réinitialise le compteur de frame, vide le cache de palettes,
        /// et réinitialise le mode précédent.
        /// </summary>
        public override void Init()
        {
            _frame = 0;
            _paletteCash.Clear();
            _noiseModePrev = Mode.Dither;
        }
        
        // =======================================================================
        // VALIDATION (appelée chaque frame)
        // =======================================================================
        
        /// <summary>
        /// Valide la passe et configure le material pour le frame courant.
        /// </summary>
        /// <param name="mat">Le material du shader à configurer.</param>
        /// <returns><c>true</c> si le rendu doit avoir lieu.</returns>
        /// <remarks>
        /// Étapes :
        /// <list type="number">
        ///   <item>Lit le <see cref="MethilDitherVol"/> depuis la pile de volumes.</item>
        ///   <item>Calcule la frame courante selon le FPS configuré.</item>
        ///   <item>Met à jour les keywords shader (PIXELATE, METHILDITHER, NOISE).</item>
        ///   <item>Génère ou récupère du cache les LUTs de palette.</item>
        ///   <item>Calcule la gigue temporelle (jitter) du motif.</item>
        ///   <item>En mode Noise, génère une texture de bruit si nécessaire.</item>
        ///   <item>Configure toutes les propriétés du material.</item>
        /// </list>
        /// </remarks>
        public override bool Validate(Material mat)
        {
            var settings = Stack.GetComponent<MethilDitherVol>();

            if (settings == null || settings.IsActive() == false)
                return false;
            
            var aspect = Screen.width / (float)Screen.height;
            
            // Calcul de la frame basé sur le FPS configuré pour la gigue
            var fps = settings.m_Fps.overrideState ? settings.m_Fps.value : _frameRate;
            var curFrame = Mathf.FloorToInt(Time.unscaledTime / (1f / fps));
            var nextFrame = _frame != curFrame;
            if (nextFrame)
                _frame = curFrame;
            
            // Détermine si la pixellisation doit être appliquée
            var pixelate = settings.m_Pixelate.overrideState ? settings.m_Pixelate.value : _pixelate;
            if ((settings.m_Scale.overrideState ? settings.m_Scale.value : _scale) >= 1f)
                pixelate = false;
            
            _validatePix(pixelate);
            
            var noiseMode = settings.m_Mode.overrideState ? settings.m_Mode.value : _noiseMode;
            _validateMode(noiseMode);
            
            // Résolution de la palette (override Volume ou défaut)
            var palette = settings.m_Palette.overrideState ? settings.m_Palette.value as Texture2D : this._palette;
            if (palette == null)
                palette = this._palette;
            
            // Génération ou récupération des LUTs pour cette palette
            if (_paletteCash.TryGetValue(palette, out var paletteCash) == false)
            {
                try
                {
                    paletteCash = LutGenerator.Generate(palette, _lutSize, _gamma);
                    _paletteCash.Add(palette, paletteCash);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[MethilDither] Failed to generate palette LUT (WebGL safe fallback): {e.Message}");
                    return false;
                }
            }
            
            var _palette = paletteCash._palette;
            var _quant   = paletteCash._quant;
            var _measure = paletteCash._measure;
            
            // Résolution du motif de dithering
            var _dither  = settings.m_Pattern.overrideState ? settings.m_Pattern.value as Texture2D : _pattern;
            if (_dither == null)
                _dither = _pattern;
            
            // Configuration des propriétés float
            mat.SetFloat(s_Dither, settings.m_Power.value);
            mat.SetFloat(s_Weight, settings.m_Impact.value);
            
            // Configuration des textures
            mat.SetTexture(s_PaletteTex, _palette);
            mat.SetTexture(s_QuantTex, _quant);
            mat.SetTexture(s_MeasureTex, _measure);
            mat.SetTexture(s_DitherTex, _dither);

            // Calcul des paramètres de motif (échelle et gigue)
            var scale        = Mathf.Lerp(_scaleRange.x, _scaleRange.y, settings.m_Scale.overrideState ? settings.m_Scale.value : _scale);
            var patternDepth = (float)(_dither.width / _dither.height);
            
            _ditherMad.x = scale * aspect;
            _ditherMad.y = scale; 
            if (nextFrame)
            {
                // Alignement sur la grille du motif en mode Dither, aléatoire pur en mode Noise
                var step = _dither.width / patternDepth;
                
                if (noiseMode == Mode.Noise)
                {
                    _ditherMad.z = Random.value;
                    _ditherMad.w = Random.value;
                }
                else
                {
                    _ditherMad.z = Mathf.Round(Random.value * step) / step;
                    _ditherMad.w = Mathf.Round(Random.value * step) / step;
                }
            }
            mat.SetVector(s_DitherMad, _ditherMad);
            mat.SetVector(s_PatternData, new Vector4(_ditherMad.x * (_dither.width / patternDepth), _ditherMad.y * _dither.height, 1f / patternDepth, patternDepth));
            
            // En mode Noise, remplace la texture de motif par du bruit
            if (noiseMode == Mode.Noise)
            {
                _validateNoise();
                
                if (_noiseTex != null)
                {
                    mat.SetTexture(s_DitherTex, _noiseTex);
                    mat.SetVector(s_DitherMad, new Vector4(_noiseScale, _noiseScale, _ditherMad.z, _ditherMad.w));
                }
            }

            return true;

            // -----------------------------------------------------------------------
            // FONCTIONS LOCALES DE VALIDATION
            // -----------------------------------------------------------------------
            
            /// <summary>
            /// Active/désactive le keyword shader PIXELATE.
            /// </summary>
            /// <param name="on"><c>true</c> pour activer la pixellisation.</param>
            void _validatePix(bool on)
            {
                if (_material.IsKeywordEnabled("PIXELATE") == on)
                    return;
                    
                if (on)
                    _material.EnableKeyword("PIXELATE");
                else
                    _material.DisableKeyword("PIXELATE");
            }
            
            /// <summary>
            /// Active/désactive les keywords shader METHILDITHER/NOISE selon le mode.
            /// </summary>
            /// <param name="mode">Mode de dithering souhaité.</param>
            /// <exception cref="ArgumentOutOfRangeException">Si le mode n'est pas reconnu.</exception>
            void _validateMode(Mode mode)
            {
                if (_noiseModePrev == mode)
                    return;
                
                _noiseModePrev = mode;
                
                _material.DisableKeyword("METHILDITHER");
                _material.DisableKeyword("NOISE");
                
                switch (mode)
                {
                    case Mode.Dither:
                        _material.EnableKeyword("METHILDITHER");
                        break;
                    case Mode.Noise:
                        _material.EnableKeyword("NOISE");
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
                }
            }
            
            /// <summary>
            /// Génère une texture de bruit à la résolution de l'écran.
            /// En WebGL, la résolution est limitée à 512px max pour éviter les saccades
            /// (<c>SetPixels</c>/<c>Apply</c> est synchrone et coûteux sur cette plateforme).
            /// </summary>
            void _validateNoise()
            {
                try
                {
                    var width  = Screen.width;
                    var height = Screen.height;

#if UNITY_WEBGL
                    // Réduction de la résolution sur WebGL pour éviter les saccades
                    const int maxSize = 512;
                    if (width > maxSize)  { height = Mathf.RoundToInt(height * (maxSize / (float)width)); width = maxSize; }
                    if (height > maxSize) { width  = Mathf.RoundToInt(width  * (maxSize / (float)height)); height = maxSize; }
#endif

                    if (_noiseTex == null || _noiseTex.width != width || _noiseTex.height != height)
                    {
                        _noiseTex            = new Texture2D(width, height);
                        _noiseTex.filterMode = FilterMode.Point;
                        _noiseTex.wrapMode   = TextureWrapMode.Repeat;
                        
                        var pix = new Color[_noiseTex.width * _noiseTex.height];
                        for (var n = 0; n < _noiseTex.width * _noiseTex.height; n++)
                        {
                            var val = Random.value > .5 ? 1f : 0f;
                            pix[n] = new Color(val, val, val, 1f);
                        }

                        _noiseTex.SetPixels(pix);
                        _noiseTex.Apply();
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[MethilDither] Failed to create noise texture (WebGL safe fallback): {e.Message}");
                    _noiseTex = null;
                }
            }
        }

        // =======================================================================
        // VALIDATION ÉDITEUR
        // =======================================================================
        
        /// <summary>
        /// Indique que la validation éditeur est nécessaire si la palette ou le motif est null.
        /// </summary>
        protected override bool _editorValidate => _palette == null || _pattern == null;
        
        /// <summary>
        /// Hook éditeur : charge les textures de palette et de motif par défaut
        /// depuis les dossiers Data/Palette et Data/Pattern.
        /// </summary>
        /// <param name="folder">Dossier contenant le shader.</param>
        /// <param name="asset">Nom du fichier shader.</param>
        protected override void _editorSetup(string folder, string asset)
        {
#if UNITY_EDITOR
            var normalizedFolder = folder.Replace('\\', '/');
            if (_palette == null)
                _palette = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>($"{normalizedFolder}/Data/Palette/dither-one-bit-bw-1x.png");
            
            if (_pattern == null)
                _pattern = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>($"{normalizedFolder}/Data/Pattern/dither-pattern-a.png");
#endif
        }
        
        // =======================================================================
        // GÉNÉRATEUR DE LUT
        // =======================================================================
        
        /// <summary>
        /// Générateur statique de LUTs de palette.
        /// Pour une palette donnée, génère trois textures LUT :
        /// <list type="bullet">
        ///   <item>Palette — pour chaque entrée de la LUT identité, la couleur de palette la plus proche</item>
        ///   <item>Quant — la deuxième couleur la plus proche (utilisée pour le dithering)</item>
        ///   <item>Measure — une valeur de distance normalisée (erreur entre les deux plus proches)</item>
        /// </list>
        /// La comparaison des couleurs utilise une pondération gamma configurable
        /// (rec601, rec709, rec2100, ou moyenne).
        /// </summary>
		public static class LutGenerator
		{
			private static Texture2D _lut16;
			private static Texture2D _lut32;
			private static Texture2D _lut64;

			/// <summary>
			/// Résolution de la LUT (nombre de samples par canal).
			/// </summary>
			[Serializable]
			public enum LutSize
			{
				x16,
				x32,
				x64
			}

			/// <summary>
			/// Courbe gamma utilisée pour comparer les couleurs.
			/// </summary>
			[Serializable]
			public enum Gamma
			{
				rec601,
				rec709,
				rec2100,
				average,
			}
			
			/// <summary>
			/// Génère les trois LUTs pour une palette donnée.
			/// </summary>
			/// <param name="_palette">Texture de palette source (ligne de pixels).</param>
			/// <param name="lutSize">Résolution de la LUT (x16 par défaut).</param>
			/// <param name="gamma">Courbe gamma pour la comparaison (rec601 par défaut).</param>
			/// <returns>Un <see cref="MethilDitherPass.PaletteCash"/> contenant les trois LUTs.</returns>
			public static MethilDitherPass.PaletteCash Generate(Texture2D _palette, LutSize lutSize = LutSize.x16, Gamma gamma = Gamma.rec601)
			{
				var clean  = _getLut(lutSize);
				var lut    = clean.GetPixels();
				var colors = _palette.GetPixels();
				
				var _lutPalette = new Texture2D(clean.width, clean.height, TextureFormat.ARGB32, false);
				var _lutQuant   = new Texture2D(clean.width, clean.height, TextureFormat.ARGB32, false);
				var _lutMeasure = new Texture2D(clean.width, clean.height, TextureFormat.ARGB32, false);

				// Pour chaque entrée de la LUT identité, trouve la couleur de palette la plus proche
				var palette = lut.Select(lutColor => colors.Select(gradeColor => (grade: compare(lutColor, gradeColor), color: gradeColor)).OrderBy(n => n.grade).First())
								.Select(n => n.color)
								.ToArray();
				
				// Pour chaque entrée, trouve la DEUXIÈME couleur la plus proche
				var quant = lut.Select(lutColor =>
							   {
								   var set = colors.Select(gradeColor => (grade: compare(lutColor, gradeColor), color: gradeColor)).OrderBy(n => n.grade).ToArray();
								   var b   = set[1];

								   return b.color;
							   })
							   .ToArray();
				
				// Calcule la mesure d'erreur normalisée entre la 1ère et 2ème couleur
				colors = _palette.GetPixels().Select(_lutAt).ToArray();
				var measure = lut.Select(lutColor =>
								{
									var set = colors.Select(gradeColor => (grade: compare(lutColor, gradeColor), color: gradeColor)).OrderBy(n => n.grade).ToArray();
									var a   = set[0];
									var b   = set[1];

									var measure = 1f - a.grade / b.grade;

									return new Color(measure, measure, measure);
								})
								.ToArray();
				
				_lutPalette.SetPixels(palette);
				_lutPalette.filterMode = FilterMode.Point;
				_lutPalette.wrapMode   = TextureWrapMode.Clamp;
				_lutPalette.Apply();
				
				_lutQuant.SetPixels(quant);
				_lutQuant.filterMode = FilterMode.Point;
				_lutQuant.wrapMode   = TextureWrapMode.Clamp;
				_lutQuant.Apply();
				
				_lutMeasure.SetPixels(measure);
				_lutMeasure.filterMode = FilterMode.Bilinear;
				_lutMeasure.wrapMode   = TextureWrapMode.Clamp;
				_lutMeasure.Apply();
				
				var result = new MethilDitherPass.PaletteCash()
				{
					_palette  = _lutPalette,
					_measure  = _lutMeasure,
					_quant    = _lutQuant,
				};
				
				return result;

				// -----------------------------------------------------------------------
				/// <summary>
				/// Compare deux couleurs selon leur distance pondérée par la courbe gamma.
				/// </summary>
				float compare(Color a, Color b)
				{
					// Pondérations selon la norme gamma choisie
					var weight = gamma switch
					{
						Gamma.rec601  => new Vector3(0.299f, 0.587f, 0.114f),
						Gamma.rec709  => new Vector3(0.2126f, 0.7152f, 0.0722f),
						Gamma.rec2100 => new Vector3(0.2627f, 0.6780f, 0.0593f),
						Gamma.average => new Vector3(0.33333f, 0.33333f, 0.33333f),
						_             => throw new ArgumentOutOfRangeException()
					};

					var c = new Vector3(a.r * weight.x, a.g * weight.y, a.b * weight.z) - new Vector3(b.r * weight.x, b.g * weight.y, b.b * weight.z);
					
					return c.magnitude;
				}
				
				/// <summary>
				/// Échantillonne la LUT à la position (r,g,b) en espace LUT normalisé.
				/// </summary>
				Color _lutAt(Color c)
				{
					if (c.r >= 1f) c.r = 0.999f;
					if (c.g >= 1f) c.g = 0.999f;
					if (c.b >= 1f) c.b = 0.999f;
					
					var _lutSize = _getLutSize(lutSize);
					var scale   = (_lutSize - 1f) / _lutSize;
					var offset  = .5f * (1f / _lutSize);
					var step    = 1f / _lutSize;
					var x = Mathf.FloorToInt((c.r * scale + offset) / step);
					var y = Mathf.FloorToInt((c.g * scale + offset) / step);
					var z = Mathf.FloorToInt((c.b * scale + offset) / step);

					return lutAt(x, y, z);
					
					Color lutAt(int x, int y, int z)
					{
						return new Color(x / (_lutSize - 1f), y / (_lutSize - 1f), z / (_lutSize - 1f), 1f);
					}
				}
			}

			/// <summary>
			/// Retourne la taille (nombre de samples par canal) pour une résolution de LUT.
			/// </summary>
			internal static int _getLutSize(LutSize lutSize)
			{
				return lutSize switch
				{
					LutSize.x16 => 16,
					LutSize.x32 => 32,
					LutSize.x64 => 64,
					_           => throw new ArgumentOutOfRangeException()
				};
			}
			
			/// <summary>
			/// Crée ou récupère du cache une LUT identité (dégradé RGB normalisé).
			/// La LUT a une taille de (size*size) x size pixels.
			/// </summary>
			internal static Texture2D _getLut(LutSize lutSize)
			{
				var size = _getLutSize(lutSize);
				var _lut = lutSize switch
				{
					LutSize.x16 => _lut16,
					LutSize.x32 => _lut32,
					LutSize.x64 => _lut64,
					_           => throw new ArgumentOutOfRangeException(nameof(lutSize), lutSize, null)
				};
				
				if (_lut != null && _lut.height == size)
					 return _lut;
				
				_lut            = new Texture2D(size * size, size, TextureFormat.RGBA32, 0, false);
				_lut.filterMode = FilterMode.Point;

				for (var y = 0; y < size; y++)
				for (var x = 0; x < size * size; x++)
					_lut.SetPixel(x, y, _lutAt(x, y));
				
				_lut.Apply();
				return _lut;

				// -----------------------------------------------------------------------
				/// <summary>
				/// Calcule la couleur à la position (x,y) dans la LUT identité.
				/// X encode R et B, Y encode G.
				/// </summary>
				Color _lutAt(int x, int y)
				{
					return new Color((x % size) / (size - 1f), y / (size - 1f), Mathf.FloorToInt(x / (float)size) * (1f / (size - 1f)), 1f);
				}
			}
		}
    }
}
