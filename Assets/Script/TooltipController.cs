using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Contrôleur global des tooltips — gère l'affichage/masquage intelligent.
/// Instance unique (singleton DDOL).
/// Détecte les positions d'éléments et positionne le tooltip de manière smart.
/// </summary>
public class TooltipController : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] private GameObject tooltipPrefab;
    [SerializeField] private GameObject tagBadgePrefab;

    [Header("Configuration")]
    [SerializeField] private float tagSpacing = 6f;           // Spacing entre les tags
    [SerializeField] private float screenPadding = 20f;       // Padding des bords écran
    [SerializeField] private float disappearDelay = 0.5f;     // Délai avant disparition
    [SerializeField] private float fadeDuration = 0.2f;       // Durée du fade-out

    private GameObject _tooltipInstance;
    private CanvasGroup _tooltipCanvasGroup;
    private Coroutine _disappearCoroutine;
    private Coroutine _monitorCoroutine;

    // État du hover : élément actuellement "survollé" + sa RectTransform
    private RectTransform _currentHoverElement;
    public RectTransform CurrentHoverElement => _currentHoverElement;

    // -----------------------------------------------
    // SINGLETON
    // -----------------------------------------------

    public static TooltipController Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // -----------------------------------------------
    // AFFICHAGE DU TOOLTIP
    // -----------------------------------------------

    /// <summary>
    /// Affiche le tooltip pour un élément (skill, équipement, module, consommable).
    /// </summary>
    public void ShowTooltip(RectTransform hoverElement, string name, string description, List<TagData> tags)
    {
        if (hoverElement == null) return;

        // Annule la disparition en cours si active
        if (_disappearCoroutine != null)
            StopCoroutine(_disappearCoroutine);

        _currentHoverElement = hoverElement;

        // Crée ou récupère le tooltip
        if (_tooltipInstance == null)
        {
            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null) return;
            _tooltipInstance = Instantiate(tooltipPrefab, canvas.transform);
            _tooltipCanvasGroup = _tooltipInstance.GetComponent<CanvasGroup>();
            if (_tooltipCanvasGroup == null)
                _tooltipCanvasGroup = _tooltipInstance.AddComponent<CanvasGroup>();
            _tooltipCanvasGroup.alpha = 1f;
        }
        else
        {
            _tooltipInstance.SetActive(true);
            _tooltipCanvasGroup.alpha = 1f;
        }

        // Remplisse le contenu
        PopulateTooltip(name, description, tags);

        // Positionne de manière intelligente
        PositionTooltipSmart(hoverElement);

        _tooltipInstance.SetActive(true);

        // Lance la coroutine de surveillance du hover
        if (_monitorCoroutine != null)
            StopCoroutine(_monitorCoroutine);
        _monitorCoroutine = StartCoroutine(MonitorTooltipHover());
    }

    /// <summary>
    /// Masque le tooltip avec un délai et fade.
    /// </summary>
    public void HideTooltip()
    {
        if (_monitorCoroutine != null)
            StopCoroutine(_monitorCoroutine);
        _monitorCoroutine = null;

        if (_disappearCoroutine != null)
            StopCoroutine(_disappearCoroutine);

        _disappearCoroutine = StartCoroutine(DisappearRoutine());
    }

    private IEnumerator DisappearRoutine()
    {
        yield return new WaitForSeconds(disappearDelay);

        // Fade-out
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            if (_tooltipCanvasGroup != null)
                _tooltipCanvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
            yield return null;
        }

        if (_tooltipInstance != null)
            _tooltipInstance.SetActive(false);

        _currentHoverElement = null;
    }

    // -----------------------------------------------
    // POPULATION DU CONTENU
    // -----------------------------------------------

    private void PopulateTooltip(string name, string description, List<TagData> tags)
    {
        // Nom
        TextMeshProUGUI nameText = _tooltipInstance.transform.Find("NameText")?.GetComponent<TextMeshProUGUI>();
        if (nameText != null)
            nameText.text = name;

        // Description
        TextMeshProUGUI descText = _tooltipInstance.transform.Find("DescriptionText")?.GetComponent<TextMeshProUGUI>();
        if (descText != null)
            descText.text = description;

        // Tags (filtrés par "affiché = true")
        Transform tagContainer = _tooltipInstance.transform.Find("TagsContainer");
        if (tagContainer != null)
        {
            // Nettoie les anciens tags
            foreach (Transform child in tagContainer)
                Destroy(child.gameObject);

            // Crée les nouveaux tags (filtrés)
            if (tags != null)
            {
                foreach (TagData tag in tags)
                {
                    if (tag == null || !tag.affiché) continue;  // Filtre

                    GameObject badgeGO = Instantiate(tagBadgePrefab, tagContainer);
                    Image badgeBG = badgeGO.GetComponent<Image>();
                    TextMeshProUGUI badgeText = badgeGO.GetComponentInChildren<TextMeshProUGUI>();

                    // DEBUG
                    Debug.Log($"[Tooltip] Badge '{tag.tagName}': Image trouvée = {badgeBG != null}, Text trouvé = {badgeText != null}");

                    if (badgeBG != null)
                    {
                        badgeBG.color = tag.Color;
                        Debug.Log($"[Tooltip] Couleur appliquée à '{tag.tagName}': {tag.Color}");
                    }
                    else
                    {
                        Debug.LogWarning($"[Tooltip] ATTENTION: Image NOT FOUND sur badge prefab pour '{tag.tagName}'");
                    }

                    if (badgeText != null)
                    {
                        badgeText.text = tag.tagName;
                        badgeText.color = tag.textColor;
                    }
                }
            }

            // Configure le spacing du container
            HorizontalLayoutGroup hlg = tagContainer.GetComponent<HorizontalLayoutGroup>();
            if (hlg != null)
                hlg.spacing = tagSpacing;
        }
    }

    // -----------------------------------------------
    // POSITIONNEMENT SMART
    // -----------------------------------------------

    /// <summary>
    /// Positionne le tooltip de manière intelligente basée sur le quadrant de l'écran.
    /// Chaque quadrant place le coin opposé du tooltip pour toucher le coin opposé de l'élément.
    /// - HAUT-GAUCHE : coin HAUT-GAUCHE du tooltip → coin BAS-DROIT de l'élément
    /// - HAUT-DROITE : coin HAUT-DROIT du tooltip → coin BAS-GAUCHE de l'élément
    /// - BAS-GAUCHE : coin BAS-GAUCHE du tooltip → coin HAUT-DROIT de l'élément
    /// - BAS-DROITE : coin BAS-DROIT du tooltip → coin HAUT-GAUCHE de l'élément
    /// </summary>
    private void PositionTooltipSmart(RectTransform hoverElement)
    {
        if (_tooltipInstance == null) return;

        RectTransform tooltipRT = _tooltipInstance.GetComponent<RectTransform>();
        if (tooltipRT == null) return;

        // Force le layout à se calculer
        LayoutRebuilder.ForceRebuildLayoutImmediate(tooltipRT);

        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        RectTransform canvasRT = canvas.GetComponent<RectTransform>();
        float canvasWidth = canvasRT.rect.width;
        float canvasHeight = canvasRT.rect.height;
        float halfWidth = canvasWidth / 2f;
        float halfHeight = canvasHeight / 2f;

        // Obtient les coins de l'élément (coordonnées monde)
        Vector3[] hoverCorners = new Vector3[4];
        hoverElement.GetWorldCorners(hoverCorners);
        // corners[0] = bas-gauche, [1] = haut-gauche, [2] = haut-droit, [3] = bas-droit

        // Convertit en coordonnées canvas locales (Screen Space - Overlay)
        Vector2[] canvasCorners = new Vector2[4];
        for (int i = 0; i < 4; i++)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRT, hoverCorners[i], canvas.worldCamera, out Vector2 localPos);
            canvasCorners[i] = localPos;
        }

        float elemLeft = canvasCorners[0].x;
        float elemRight = canvasCorners[2].x;
        float elemTop = canvasCorners[1].y;
        float elemBottom = canvasCorners[0].y;
        float elemCenterX = (elemLeft + elemRight) / 2f;
        float elemCenterY = (elemTop + elemBottom) / 2f;

        Vector2 tooltipSize = tooltipRT.rect.size;

        // Calcule le padding adaptatif basé sur la taille de l'élément
        float elemWidth = elemRight - elemLeft;
        float elemHeight = elemTop - elemBottom;
        float elementSize = (elemWidth + elemHeight) / 2f;
        float adaptivePadding = Mathf.Clamp(elementSize * 0.15f, 2f, 10f);

        // Détermine le quadrant et sélectionne le pivot + la position de base
        Vector2 pivot = Vector2.zero;
        Vector2 tooltipPos = Vector2.zero;

        if (elemCenterX < 0 && elemCenterY > 0)
        {
            // HAUT-GAUCHE : coin HAUT-GAUCHE du tooltip touche coin BAS-DROIT de l'élément
            pivot = new Vector2(0, 1);
            tooltipPos = new Vector2(elemRight + adaptivePadding, elemBottom);
        }
        else if (elemCenterX >= 0 && elemCenterY > 0)
        {
            // HAUT-DROITE : coin HAUT-DROIT du tooltip touche coin BAS-GAUCHE de l'élément
            pivot = new Vector2(1, 1);
            tooltipPos = new Vector2(elemLeft - adaptivePadding, elemBottom);
        }
        else if (elemCenterX < 0 && elemCenterY <= 0)
        {
            // BAS-GAUCHE : coin BAS-GAUCHE du tooltip touche coin HAUT-DROIT de l'élément
            pivot = new Vector2(0, 0);
            tooltipPos = new Vector2(elemRight + adaptivePadding, elemTop);
        }
        else
        {
            // BAS-DROITE : coin BAS-DROIT du tooltip touche coin HAUT-GAUCHE de l'élément
            pivot = new Vector2(1, 0);
            tooltipPos = new Vector2(elemLeft - adaptivePadding, elemTop);
        }

        // Assigne le pivot
        tooltipRT.pivot = pivot;

        // Clamp X adapté au pivot
        float minX, maxX;
        if (pivot.x == 0)  // Pivot au coin GAUCHE
        {
            minX = -halfWidth + screenPadding;
            maxX = halfWidth - screenPadding - tooltipSize.x;
        }
        else  // Pivot au coin DROIT (pivot.x == 1)
        {
            minX = -halfWidth + screenPadding + tooltipSize.x;
            maxX = halfWidth - screenPadding;
        }

        // Clamp Y adapté au pivot.y
        float minY, maxY;
        if (pivot.y == 0)  // Pivot au coin BAS
        {
            minY = -halfHeight + screenPadding;
            maxY = halfHeight - screenPadding - tooltipSize.y;
        }
        else  // Pivot au coin HAUT (pivot.y == 1)
        {
            minY = -halfHeight + screenPadding + tooltipSize.y;
            maxY = halfHeight - screenPadding;
        }

        tooltipPos.x = Mathf.Clamp(tooltipPos.x, minX, maxX);
        tooltipPos.y = Mathf.Clamp(tooltipPos.y, minY, maxY);

        tooltipRT.anchoredPosition = tooltipPos;
    }

    // -----------------------------------------------
    // GESTION DE LA SORTIE DU SURVOL
    // -----------------------------------------------

    /// <summary>
    /// Appelé quand la souris quitte l'élément survolé.
    /// Vérifie d'abord si la souris est sur le tooltip avant de le masquer.
    /// Cela évite le flickering lors du déplacement souris vers le tooltip.
    /// </summary>
    public void OnHoverElementExit()
    {
        if (_tooltipInstance == null || !_tooltipInstance.activeSelf)
            return;

        // Vérifie si la souris est actuellement sur le tooltip
        bool mouseOnTooltip = RectTransformUtility.RectangleContainsScreenPoint(
            _tooltipInstance.GetComponent<RectTransform>(), Input.mousePosition);

        // Masque le tooltip seulement si la souris n'est PAS dessus
        if (!mouseOnTooltip)
            HideTooltip();
    }

    // -----------------------------------------------
    // SURVEILLANCE DU HOVER (COROUTINE LÉGÈRE)
    // -----------------------------------------------

    /// <summary>
    /// Coroutine de surveillance : vérifie tous les 0.1s si la souris reste
    /// sur l'élément survolé ou sur le tooltip.
    /// Beaucoup moins coûteux qu'un Update() à chaque frame (60+ fois/sec).
    /// </summary>
    private IEnumerator MonitorTooltipHover()
    {
        while (_tooltipInstance != null && _tooltipInstance.activeSelf)
        {
            yield return new WaitForSeconds(0.1f);

            // Vérifie si la souris est sur l'élément survolé
            bool mouseOnElement = _currentHoverElement != null
                && RectTransformUtility.RectangleContainsScreenPoint(_currentHoverElement, Input.mousePosition);

            // Vérifie si la souris est sur le tooltip
            bool mouseOnTooltip = RectTransformUtility.RectangleContainsScreenPoint(
                _tooltipInstance.GetComponent<RectTransform>(), Input.mousePosition);

            // Si la souris n'est sur aucun des deux, masque le tooltip
            if (!mouseOnElement && !mouseOnTooltip)
            {
                HideTooltip();
                break;
            }
        }
    }
}
