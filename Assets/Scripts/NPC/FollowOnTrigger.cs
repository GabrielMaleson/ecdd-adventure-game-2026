using UnityEngine;

// "Ao pisar aqui, eles voltam a te seguir." (ou param de seguir)
//
// Existe porque religar o follow so era possivel de dentro do .yarn (<<follow Marcus>>), e
// nem todo momento em que a fila deve se formar tem uma fala junto. Sair da area do puzzle
// e exatamente isso: nao ha o que dizer, os tres so voltam a andar juntos.
//
// Os nomes sao os mesmos do <<follow>> — o Follower Name de cada NpcFollow, ou o nome do
// objeto quando aquele campo esta vazio. Nao ha referencia a objeto nenhum aqui, entao um
// personagem trocado de prefab continua funcionando.
//
// O NpcFollow ja sabe entrar na fila ANDANDO e ja se reassenta quando a distancia e grande
// demais para ser andada, entao este componente nao precisa se preocupar com onde eles
// estao quando o gatilho dispara.
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class FollowOnTrigger : MonoBehaviour
{
    [Header("Quem")]
    [Tooltip("Nomes de NpcFollow que passam a seguir. Ex: Marcus, Erika.")]
    public string[] passamASeguir;

    [Tooltip("Nomes de NpcFollow que PARAM de seguir. Util no gatilho de entrada de uma " +
             "area onde eles devem ficar para tras.")]
    public string[] param;

    [Header("Quando")]
    public string playerTag = "Player";

    [Tooltip("Dispara uma vez so.")]
    public bool onceOnly = true;

    [Tooltip("So dispara com este progresso gravado (SaveManager). Vazio = sem condicao.")]
    public string requiresProgress = "";

    private bool fired;

    private void Reset()
    {
        Collider2D c = GetComponent<Collider2D>();
        if (c != null) c.isTrigger = true;
    }

    private void Awake()
    {
        Collider2D c = GetComponent<Collider2D>();
        if (c != null && !c.isTrigger)
            Debug.LogWarning($"[FollowOnTrigger] '{name}': o Collider2D nao esta marcado Is " +
                             "Trigger. Sem isso ele vira parede e nunca dispara.", this);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (fired && onceOnly) return;
        if (other == null || !other.CompareTag(playerTag)) return;

        if (!string.IsNullOrEmpty(requiresProgress))
        {
            if (SaveManager.Instance == null) return;
            if (!SaveManager.Instance.HasProgress(requiresProgress)) return;
        }

        fired = true;

        if (param != null)
            foreach (string quem in param)
                if (!string.IsNullOrEmpty(quem)) NpcFollow.StopFollowing(quem);

        if (passamASeguir != null)
            foreach (string quem in passamASeguir)
                if (!string.IsNullOrEmpty(quem)) NpcFollow.StartFollowing(quem);
    }
}
