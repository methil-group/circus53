using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Force un ratio d'affichage fixe (4:3 par défaut) avec letterboxing/pillarboxing automatique.
/// 
/// Fonctionnement :
/// - S'attache à la caméra de jeu principale (tag "MainCamera")
/// - Crée automatiquement une caméra secondaire pour les barres noires
/// - Ajuste le viewport rect à chaque changement de résolution
/// 
/// Pillarboxing : écran trop large  → barres noires à gauche/droite
/// Letterboxing : écran trop haut    → barres noires en haut/bas
/// </summary>
[RequireComponent(typeof(Camera))]
[DisallowMultipleComponent]
public class CameraAspectRatio : MonoBehaviour
{
    [Header("Ratio cible")]
    [SerializeField, Tooltip("Ratio largeur/hauteur. 4:3 = 1.333")]
    private float _targetAspect = 4f / 3f;
    
    [SerializeField, Tooltip("Couleur des barres (noir par défaut)")]
    private Color _barColor = Color.black;
    
    private Camera _gameCamera;
    private Camera _barCamera;
    private int    _lastWidth;
    private int    _lastHeight;
    
    // =======================================================================
    
    private void Awake()
    {
        _gameCamera = GetComponent<Camera>();
        
        // Crée une caméra dédiée au remplissage des barres noires
        CreateBarCamera();
        
        ForceUpdate();
    }
    
    private void LateUpdate()
    {
        if (Screen.width != _lastWidth || Screen.height != _lastHeight)
            ForceUpdate();
    }
    
    // =======================================================================
    
    /// <summary>
    /// Force la mise à jour immédiate du viewport (utile après un changement de résolution).
    /// </summary>
    [ContextMenu("Force Update")]
    public void ForceUpdate()
    {
        _lastWidth  = Screen.width;
        _lastHeight = Screen.height;
        
        float currentAspect = (float)Screen.width / Screen.height;
        
        if (currentAspect > _targetAspect)
        {
            // Pillarboxing : barres noires verticales à gauche/droite
            float scaleWidth = _targetAspect / currentAspect;
            float inset      = (1f - scaleWidth) / 2f;
            _gameCamera.rect = new Rect(inset, 0f, scaleWidth, 1f);
        }
        else
        {
            // Letterboxing : barres noires horizontales en haut/bas
            float scaleHeight = currentAspect / _targetAspect;
            float inset       = (1f - scaleHeight) / 2f;
            _gameCamera.rect  = new Rect(0f, inset, 1f, scaleHeight);
        }
    }
    
    /// <summary>
    /// Crée une caméra de fond pour les barres noires.
    /// Rendu plein écran, couleur unie, depth inférieure à la caméra de jeu.
    /// </summary>
    private void CreateBarCamera()
    {
        var barGo = new GameObject("[AspectRatio] Bar Camera");
        barGo.transform.SetParent(transform);
        barGo.hideFlags = HideFlags.NotEditable;
        
        _barCamera = barGo.AddComponent<Camera>();
        _barCamera.clearFlags      = CameraClearFlags.SolidColor;
        _barCamera.backgroundColor = _barColor;
        _barCamera.cullingMask     = 0;              // Ne rend aucun objet
        _barCamera.depth           = _gameCamera.depth - 1f;
        _barCamera.useOcclusionCulling = false;
        _barCamera.allowHDR        = false;
        _barCamera.allowMSAA       = false;
        _barCamera.rect            = new Rect(0f, 0f, 1f, 1f); // Plein écran
        
        // URP : désactiver le post-processing et le rendu inutile sur cette caméra
        var barData = barGo.AddComponent<UniversalAdditionalCameraData>();
        barData.renderPostProcessing   = false;
        barData.requiresColorTexture   = false;
        barData.requiresDepthTexture   = false;
        barData.renderShadows          = false;
        barData.antialiasing           = AntialiasingMode.None;
        barData.stopNaN                = false;
        barData.dithering              = false;
        
        // Désactiver l'AudioListener ajouté automatiquement par Unity
        var audioListener = barGo.GetComponent<AudioListener>();
        if (audioListener != null)
            Destroy(audioListener);
    }
    
    // =======================================================================
    
#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying && _gameCamera != null)
            ForceUpdate();
    }
#endif
}
