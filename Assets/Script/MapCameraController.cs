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

    private void HandleZoom()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Approximately(scroll, 0f)) return;

        // Calcule le nouveau zoom
        CurrentZoom = Mathf.Clamp(CurrentZoom + scroll * zoomSpeed * 10f, zoomMin, zoomMax);
        mapContainer.localScale = new Vector3(CurrentZoom, CurrentZoom, 1f);
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