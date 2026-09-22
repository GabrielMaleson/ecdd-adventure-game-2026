using UnityEngine;

/// <summary>
/// Silencia o som de passo do jogador enquanto ele estiver dentro desta zona.
/// Mesmo padrão do ZoneMusicTrigger: Collider2D marcado como Trigger cobrindo o chão
/// do interior; ao entrar, desliga os passos; ao sair, liga de volta.
///
/// Setup:
///   1. Adicione BoxCollider2D (Is Trigger) cobrindo o piso da casa/interior.
///   2. Adicione este script no mesmo GameObject.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class IndoorZone : MonoBehaviour
{
    private void Start()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (!col.isTrigger)
        {
            Debug.LogWarning($"[IndoorZone] O Collider2D em '{gameObject.name}' não está marcado como Trigger. Marque Is Trigger para que a detecção funcione.");
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        other.GetComponentInParent<FootstepPlayer>()?.SetIndoors(true);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        other.GetComponentInParent<FootstepPlayer>()?.SetIndoors(false);
    }
}
