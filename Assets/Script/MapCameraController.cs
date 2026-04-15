using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Gère le zoom (molette) et le déplacement (clic maintenu + glisser)
/// de la vue sur la carte de navigation.
/// S'attache au RectTransform du conteneur de la carte.
/// </summary>
public class MapCameraController : MonoBehaviour
{
    [Header("Références")]
    // Le RectTransform du conteneur de la carte à déplacer/zoomer
    public RectTransform mapContainer;

    // Canvas parent — nécessaire pour convertir la position souris en coordonnées locales
    [SerializeField] private Canvas canvasParent;

    [Header("Zoom")]
    public float zoomSpeed    = 0.1f;
    public float zoomMin      = 0.3f;
    public float zoomMax      = 3.0f;

    [Header("Déplacement")]
    public float dragSpeed = 1.0f;

    // État interne du drag
    private bool   isDragging      = false;
    private Vector2 lastMousePosition;

    // Échelle actuelle du conteneur
    public float CurrentZoom { get; private set; } = 1.0f;

    void Update()
    {
        HandleZoom();
        HandleDrag();
    }

    // -----------------------------------------------
    // ZOOM MOLETTE
    // -----------------------------------------------

    /// <summary>
    /// Applique le zoom molette en gardant le point de la carte
    /// sous le curseur fixe à l'écran.
    ///
    /// Mécanique :
    ///   1. Convertir la position souris en coordonnées locales du mapContainer (avant zoom).
    ///   2. Appliquer la nouvelle échelle.
    ///   3. Décaler anchoredPosition de (positionLocale * deltaZoom) pour compenser
    ///      le déplacement apparent introduit par le changement d'échelle.
    /// </summary>
    private void HandleZoom()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Approximately(scroll, 0f)) return;

        // --- 1. Position souris en coordonnées locales du mapContainer avant zoom ---
        Camera camCanvas = canvasParent != null && canvasParent.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvasParent.worldCamera
            : null;

        Vector2 positionLocaleSouris;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            mapContainer,
            Input.mousePosition,
            camCanvas,
            out positionLocaleSouris
        );

        // --- 2. Calcul et application du nouveau zoom ---
        float ancienZoom = CurrentZoom;
        CurrentZoom = Mathf.Clamp(CurrentZoom + scroll * zoomSpeed * 10f, zoomMin, zoomMax);
        mapContainer.localScale = new Vector3(CurrentZoom, CurrentZoom, 1f);

        // --- 3. Compensation de position pour garder le point sous le curseur fixe ---
        float deltaZoom = CurrentZoom - ancienZoom;
        mapContainer.anchoredPosition -= positionLocaleSouris * deltaZoom;
    }
    // -----------------------------------------------
    // DÉPLACEMENT CLIC MAINTENU
    // -----------------------------------------------

    private void HandleDrag()
    {
        // Début du drag — clic gauche enfoncé
        if (Input.GetMouseButtonDown(0))
        {
            isDragging        = true;
            lastMousePosition = Input.mousePosition;
        }

        // Fin du drag
        if (Input.GetMouseButtonUp(0))
            isDragging = false;

        // Déplacement en cours
        if (!isDragging) return;

        Vector2 currentMousePosition = Input.mousePosition;
        Vector2 delta = (currentMousePosition - lastMousePosition) * dragSpeed;

        mapContainer.anchoredPosition += delta;

        lastMousePosition = currentMousePosition;
    }
}