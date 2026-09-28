using UnityEngine;
using UnityEngine.InputSystem;

// Point 9 :
// - Permet à la caméra de se déplacer librement autour et au-dessus du terrain (vol libre).
// - Propose une interaction pour faire tourner le terrain, caméra fixe (flèches gauche/droite).
public class CameraTerrainController : MonoBehaviour
{
    [Header("Cible")]
    [Tooltip("Le transform du terrain à faire tourner (flèches gauche/droite)")]
    public Transform terrain;

    [Header("Déplacement caméra (vol libre)")]
    public float vitesseDeplacement = 20f;
    public float vitesseDeplacementRapide = 50f; // maintenir Shift
    public float sensibiliteSouris = 2f;         // actif en maintenant le clic droit

    [Header("Rotation du terrain (caméra fixe)")]
    public float vitesseRotationTerrain = 45f; // degrés / seconde

    private float p_rotationYaw;
    private float p_rotationPitch;

    void Start()
    {
        Vector3 anglesActuels = transform.eulerAngles;
        p_rotationYaw = anglesActuels.y;
        p_rotationPitch = anglesActuels.x;
    }

    void Update()
    {
        deplacerCamera();
        regarderAvecSouris();
        tournerLeTerrain();
    }

    // ZQSD/WASD + Espace/Ctrl pour monter/descendre, Shift pour accélérer
    private void deplacerCamera()
    {
        if (Keyboard.current == null) return;

        Vector3 direction = Vector3.zero;
        if (Keyboard.current.wKey.isPressed || Keyboard.current.zKey.isPressed) direction += transform.forward;
        if (Keyboard.current.sKey.isPressed) direction -= transform.forward;
        if (Keyboard.current.aKey.isPressed || Keyboard.current.qKey.isPressed) direction -= transform.right;
        if (Keyboard.current.dKey.isPressed) direction += transform.right;
        if (Keyboard.current.spaceKey.isPressed || Keyboard.current.eKey.isPressed) direction += Vector3.up;
        if (Keyboard.current.leftCtrlKey.isPressed) direction -= Vector3.up;

        if (direction.sqrMagnitude < 0.0001f) return;

        float vitesse = Keyboard.current.leftShiftKey.isPressed ? vitesseDeplacementRapide : vitesseDeplacement;
        transform.position += direction.normalized * vitesse * Time.deltaTime;
    }

    // Clic droit maintenu + déplacement souris = orientation de la caméra (comme dans la Scene view Unity)
    private void regarderAvecSouris()
    {
        if (Mouse.current == null || !Mouse.current.rightButton.isPressed) return;

        Vector2 delta = Mouse.current.delta.ReadValue();
        p_rotationYaw += delta.x * sensibiliteSouris * Time.deltaTime;
        p_rotationPitch -= delta.y * sensibiliteSouris * Time.deltaTime;
        p_rotationPitch = Mathf.Clamp(p_rotationPitch, -89f, 89f);

        transform.eulerAngles = new Vector3(p_rotationPitch, p_rotationYaw, 0f);
    }

    // Flèches gauche/droite : fait tourner le terrain autour de son axe Y, caméra fixe
    private void tournerLeTerrain()
    {
        if (terrain == null || Keyboard.current == null) return;

        if (Keyboard.current.leftArrowKey.isPressed)
            terrain.Rotate(Vector3.up, -vitesseRotationTerrain * Time.deltaTime, Space.World);

        if (Keyboard.current.rightArrowKey.isPressed)
            terrain.Rotate(Vector3.up, vitesseRotationTerrain * Time.deltaTime, Space.World);
    }
}