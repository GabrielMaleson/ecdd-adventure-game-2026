using UnityEngine;

/// <summary>
/// Troca a música de fundo ao entrar e sair de uma zona.
/// Adicione ao mesmo GameObject que tem um Collider2D marcado como Trigger.
///
/// Setup:
///   1. Crie um GameObject vazio na cena.
///   2. Adicione BoxCollider2D (marque Is Trigger).
///   3. Adicione este script.
///   4. Atribua musicaAoEntrar e, opcionalmente, musicaAoSair no inspector.
///   5. Os nomes devem corresponder aos nomes cadastrados em MusicManager > musicTracks.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class ZoneMusicTrigger : MonoBehaviour
{
    [Tooltip("Nome da trilha a tocar quando o jogador entrar nesta zona. Deve existir em MusicManager > musicTracks.")]
    public string musicaAoEntrar;

    [Tooltip("Nome da trilha a tocar quando o jogador sair desta zona. Deixe em branco para não trocar ao sair.")]
    public string musicaAoSair;

    private void Start()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (!col.isTrigger)
        {
            Debug.LogWarning($"[ZoneMusicTrigger] O Collider2D em '{gameObject.name}' não está marcado como Trigger. Marque Is Trigger para que a detecção funcione.");
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        if (!string.IsNullOrEmpty(musicaAoEntrar))
            MusicManager.PlayMusicCommand(musicaAoEntrar);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        if (!string.IsNullOrEmpty(musicaAoSair))
            MusicManager.PlayMusicCommand(musicaAoSair);
    }
}
