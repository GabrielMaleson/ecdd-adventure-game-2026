using UnityEngine;

/// <summary>
/// Toca Grass_Footsteps (ou qualquer clipe de passo) em intervalos enquanto o jogador
/// anda. Só lê PlayerController.MoveDirection, que já é público — não precisa mexer
/// no PlayerController para isto.
///
/// Coloque no mesmo objeto do PlayerController e arraste o clipe no Inspector.
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class FootstepPlayer : MonoBehaviour
{
    [Tooltip("Som de passo tocado enquanto o jogador se move.")]
    public AudioClip footstepClip;

    [Tooltip("Passos por segundo.")]
    [Min(0.01f)]
    public float stepsPerSecond = 3f;

    [Tooltip("Volume do passo (0 a 1). Grass_Footsteps.mp3 é normalizado no import, então soa mais alto do que outros SFX no volume cheio.")]
    [Range(0f, 1f)]
    public float volume = 0.5f;

    private PlayerController controller;
    private float stepTimer;
    private bool indoors;

    private void Awake()
    {
        controller = GetComponent<PlayerController>();
    }

    /// <summary>Liga/desliga o som de passo. Chamado por IndoorZone ao entrar/sair de um interior.</summary>
    public void SetIndoors(bool value) => indoors = value;

    private void Update()
    {
        if (indoors || controller.MoveDirection == Vector2.zero)
        {
            // Zerado para que o PRÓXIMO passo toque assim que ele voltar a andar,
            // em vez de esperar o intervalo inteiro de novo.
            stepTimer = 0f;
            return;
        }

        stepTimer += Time.deltaTime;
        float interval = 1f / stepsPerSecond;
        if (stepTimer < interval) return;

        stepTimer -= interval;
        SFXManager.Instance?.Play(footstepClip, volume);
    }
}
