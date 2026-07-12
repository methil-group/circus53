/// <summary>
/// Fichier : Utils.cs
/// Classe statique de fonctions utilitaires utilisées dans tout le plugin MethilDither.
/// Fournit des helpers pour le rendu fullscreen (mesh, blit), des extensions mathématiques
/// et des helpers de conversion de types.
/// </summary>
/// <remarks>
/// Points importants :
/// <list type="bullet">
///   <item><see cref="FullscreenMesh"/> utilise un <b>triangle fullscreen</b> plutôt qu'un quad :
///       un triangle couvre tout l'écran avec 3 vertices au lieu de 4, évitant la diagonale
///       superflue et les artefacts de bord.</item>
///   <item>Les UV sont ajustés selon <see cref="SystemInfo.graphicsUVStartsAtTop"/> pour la
///       compatibilité multiplateforme (DirectX vs OpenGL).</item>
///   <item><see cref="Blit"/> est la méthode principale pour appliquer un material shader
///       sur une render texture.</item>
/// </list>
/// </remarks>
#if !VOL_FX

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Random = UnityEngine.Random;

namespace VolFx
{
    /// <summary>
    /// Utilitaires statiques pour le rendu, les maths et les conversions.
    /// </summary>
    public static class Utils
    {
        /// <summary>ID de la propriété shader _MainTex.</summary>
        public static int s_MainTexId = Shader.PropertyToID("_MainTex");
        
        /// <summary>Mesh fullscreen triangle (initialisé paresseusement).</summary>
        private static Mesh s_FullscreenQuad;
        /// <summary>Mesh fullscreen triangle (initialisé paresseusement).</summary>
        private static Mesh s_FullscreenTriangle;

        /// <summary>
        /// Mesh fullscreen triangle optimisé pour les passes de post-processing.
        /// Préféré au quad car il évite la diagonale inutile et les coutures.
        /// </summary>
        public static  Mesh FullscreenMesh
        {
            get
            {
                _initFullScreenMeshes();
                return s_FullscreenTriangle;
            }
        }

        /// <summary>Matrice de transformation avec Y inversé (pour plateformes où UV(0,0) est en haut).</summary>
        public static Matrix4x4 s_IndentityInvert = new Matrix4x4(new Vector4(1, 0, 0, 0), new Vector4(0, -1, 0, 0), new Vector4(0, 0, 1, 0), new Vector4(0, 0, 0, 1));

        /// <summary>
        /// Initialise les meshes fullscreen (quad et triangle).
        /// Appelé une seule fois, de façon paresseuse.
        /// </summary>
        private static void _initFullScreenMeshes()
        {
            // Fullscreen Quad (4 vertices, 2 triangles)
            if (s_FullscreenQuad == null)
            {
                s_FullscreenQuad = new Mesh { name = "Fullscreen Quad" };
                s_FullscreenQuad.SetVertices(new List<Vector3>
                {
                    new Vector3(-1.0f, -1.0f, 0.0f),
                    new Vector3(-1.0f, 1.0f, 0.0f),
                    new Vector3(1.0f, -1.0f, 0.0f),
                    new Vector3(1.0f, 1.0f, 0.0f)
                });

                s_FullscreenQuad.SetUVs(0, new List<Vector2>
                {
                    new Vector2(0.0f, 1f),
                    new Vector2(0.0f, 0f),
                    new Vector2(1.0f, 1f),
                    new Vector2(1.0f, 0f)
                });

                s_FullscreenQuad.SetIndices(new[] { 0, 1, 2, 2, 1, 3 }, MeshTopology.Triangles, 0, false);
                s_FullscreenQuad.UploadMeshData(true);
            }
            
            // Fullscreen Triangle (3 vertices, 1 triangle — plus efficace)
            if (s_FullscreenTriangle == null)
            { 
                s_FullscreenTriangle           = new Mesh() { name = "Fullscreen Triangle" };
                s_FullscreenTriangle.vertices  = _verts(0f);
                s_FullscreenTriangle.uv        = _texCoords();
                s_FullscreenTriangle.triangles = new int[3] { 0, 1, 2 };

                s_FullscreenTriangle.UploadMeshData(true);

                /// <summary>
                /// Génère les 3 vertices du triangle fullscreen.
                /// </summary>
                Vector3[] _verts(float z)
                {
                    var r = new Vector3[3];
                    for (var i = 0; i < 3; i++)
                    {
                        var uv = new Vector2((i << 1) & 2, i & 2);
                        r[i] = new Vector3(uv.x * 2f - 1f, uv.y * 2f - 1f, z);
                    }

                    return r;
                }

                /// <summary>
                /// Génère les coordonnées UV en respectant l'orientation de la plateforme.
                /// </summary>
                Vector2[] _texCoords()
                {
                    var r = new Vector2[3];
                    for (var i = 0; i < 3; i++)
                    {
                        if (SystemInfo.graphicsUVStartsAtTop)
                            r[i] = new Vector2((i << 1) & 2, 1.0f - (i & 2));
                        else
                            r[i] = new Vector2((i << 1) & 2, i & 2);
                    }

                    return r;
                }
            }
        }
        
        /// <summary>
        /// Effectue un blit (copie avec material) d'une source vers une destination.
        /// </summary>
        /// <param name="cmd">CommandBuffer actif.</param>
        /// <param name="source">Texture source.</param>
        /// <param name="destination">Texture de destination.</param>
        /// <param name="material">Material à appliquer (peut être null).</param>
        /// <param name="pass">Index de la passe shader à utiliser.</param>
        /// <param name="invert">
        /// Si <c>true</c>, utilise <see cref="s_IndentityInvert"/> pour inverser l'axe Y
        /// (nécessaire sur certaines plateformes).
        /// </param>
        public static void Blit(CommandBuffer cmd, RTHandle source, RTHandle destination, Material material, int pass = 0, bool invert = false)
        {
            cmd.SetGlobalTexture(s_MainTexId, source);
            cmd.SetRenderTarget(destination, 0);
            cmd.DrawMesh(FullscreenMesh, invert ? s_IndentityInvert : Matrix4x4.identity, material, 0, pass);
        }
        
        // =======================================================================
        // Extensions mathématiques
        // =======================================================================
        
        /// <summary>Convertit un angle en radians vers un Vector2 directionnel (cos, sin).</summary>
        public static Vector2 ToNormal(this float rad)
        {
            return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        }
        
        /// <summary>Arrondit un float à l'entier le plus proche.</summary>
        public static float Round(this float f)
        {
            return Mathf.Round(f);
        }
        
        /// <summary>Clampe un float entre 0 et 1.</summary>
        public static float Clamp01(this float f)
        {
            return Mathf.Clamp01(f);
        }
        
        /// <summary>Retourne 1 - f (complément à 1).</summary>
        public static float OneMinus(this float f)
        {
            return 1f - f;
        }
        
        /// <summary>Remappe linéairement une valeur de [0,1] vers [min, max].</summary>
        public static float Remap(this float f, float min, float max)
        {
            return min + (max - min) * f;
        }
        
        /// <summary>Génère une couleur aléatoire opaque.</summary>
        public static Color Color()
        {
            return new Color(Random.Range(0.0f, 1.0f), Random.Range(0.0f, 1.0f),
                             Random.Range(0.0f, 1.0f), 1.0f);
        }
        
        /// <summary>Remplace la composante Z d'un Vector3.</summary>
        public static Vector3 WithZ(this Vector3 vector, float z)
        {
            return new Vector3(vector.x, vector.y, z);
        }
        
        /// <summary>Convertit un Vector3 en Vector2 (garde X, Y).</summary>
        public static Vector2 To2DXY(this Vector3 vector)
        {
            return new Vector2(vector.x, vector.y);
        }
        
        /// <summary>Convertit un Vector2 en Vector3 (X→X, Y→Z, Y par défaut=0).</summary>
        public static Vector3 To3DXZ(this Vector2 vector)
        {
            return vector.To3DXZ(0);
        }
        
        /// <summary>Convertit un Vector2 en Vector3 (X→X, Y→Z) avec Y spécifié.</summary>
        public static Vector3 To3DXZ(this Vector2 vector, float y)
        {
            return new Vector3(vector.x, y, vector.y);
        }

        /// <summary>Convertit un Vector2 en Vector3 (X→X, Y→Y) avec Z spécifiée.</summary>
        public static Vector3 To3DXY(this Vector2 vector, float z)
        {
            return new Vector3(vector.x, vector.y, z);
        }
        
        /// <summary>Crée un Vector2 avec les deux composantes égales à la valeur.</summary>
        public static Vector2 ToVector2XY(this float value)
        {
            return new Vector2(value, value);
        }
        
        /// <summary>Multiplie uniquement le canal alpha d'une couleur.</summary>
        public static Color MulA(this Color color, float a)
        {
            return new Color(color.r, color.g, color.b, color.a * a);
        }
        
        /// <summary>Retourne un Rect couvrant toute la texture (0,0,width,height).</summary>
        public static Rect GetRect(this Texture2D texture)
        {
            return new Rect(0, 0, texture.width, texture.height);
        }
        
        /// <summary>Arrondit un float à l'entier le plus proche (retourne int).</summary>
        public static int RoundToInt(this float f)
        {
            return Mathf.RoundToInt(f);
        }
        
        /// <summary>
        /// Retourne la valeur maximale d'une séquence selon un sélecteur, ou la valeur par défaut si la séquence est vide.
        /// Version retournant la clé.
        /// </summary>
        public static TKey MaxOrDefault<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> selector, TSource noOptionsValue = default)
        {
            var result = source.MaxOrDefault(selector, Comparer<TKey>.Default, noOptionsValue);
            if (Equals(result, default))
                return default;
            
            return selector(result);
        }

        /// <summary>
        /// Retourne l'élément avec la valeur maximale selon un sélecteur et un comparateur,
        /// ou un fallback si la séquence est vide. Version retournant la source.
        /// </summary>
        public static TSource MaxOrDefault<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> selector, IComparer<TKey> comparer, TSource fallback = default)
        {
            using (var sourceIterator = source.GetEnumerator())
            {
                if (sourceIterator.MoveNext() == false)
                    return fallback;

                var max = sourceIterator.Current;
                var maxKey = selector(max);
		
                while (sourceIterator.MoveNext())
                {
                    var candidate = sourceIterator.Current;
                    var candidateProjected = selector(candidate);

                    if (comparer.Compare(candidateProjected, maxKey) > 0)
                    {
                        max = candidate;
                        maxKey = candidateProjected;
                    }
                }
                return max;
            }
        }
    }
}

#endif
