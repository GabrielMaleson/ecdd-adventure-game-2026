using UnityEngine;

// Minimal 2D camera follow. Replaces parenting the camera under the MC, so the
// SAME camera can switch between tracking the MC and tracking the ghost:
// GhostControl's onCameraTargetChanged event calls SetTarget with whichever the
// camera should currently look at.
public class CameraFollow2D : MonoBehaviour
{
    [Tooltip("Who the camera tracks. GhostControl overrides this at runtime; still assign the MC here so frame zero is framed before the first event fires.")]
    [SerializeField] Transform target;

    [Tooltip("World offset from the target. Keep Z negative (e.g. -10) so a 2D camera stays in front. Match X/Y to the framing you had while the camera was a child of the MC.")]
    [SerializeField] Vector3 offset = new Vector3(0f, 0f, -10f);

    [Tooltip("Seconds to ease toward the target while FOLLOWING someone. 0 = locked instantly (exactly like being parented). Keep this small: a big value here makes the camera drag behind the MC while he walks.")]
    [SerializeField] float smoothTime = 0.15f;

    [Tooltip("Segundos de suavizacao enquanto a camera esta TROCANDO de alvo. Maior que o " +
             "de cima de proposito. Trocar de alvo e uma viagem, nao um acompanhamento: o " +
             "Josh pode estar a dez metros da Haze, e a mesma suavizacao que fica boa " +
             "seguindo alguem produz um corte seco atravessando essa distancia. Este numero " +
             "so vale enquanto a viagem dura.")]
    [SerializeField] float smoothTimeAoTrocar = 0.45f;

    [Tooltip("Quando a viagem acaba e a suavizacao normal volta: distancia ate o alvo, em unidades. Perto o bastante para ninguem notar a troca de numero.")]
    [SerializeField] float distanciaParaTerminarTroca = 0.4f;

    Vector3 velocity;
    bool trocando;

    // Wire GhostControl.onCameraTargetChanged here (pick SetTarget under the
    // dynamic Transform section so it passes the target through automatically).
    public void SetTarget(Transform t)
    {
        // So conta como troca se for outra pessoa. Reapontar para quem ja esta sendo
        // seguido nao deve amolecer a camera do nada.
        if (t != target) trocando = true;
        target = t;
    }

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 desired = target.position + offset;

        // A viagem termina por DISTANCIA e nao por tempo: um alvo perto chega rapido e um
        // longe demora, e nos dois casos a suavizacao normal volta no momento em que a
        // camera de fato alcancou — sem numero de segundos para acertar por tentativa.
        if (trocando && (transform.position - desired).sqrMagnitude <=
            distanciaParaTerminarTroca * distanciaParaTerminarTroca)
            trocando = false;

        float suavizacao = trocando ? smoothTimeAoTrocar : smoothTime;

        if (suavizacao <= 0f)
            transform.position = desired;
        else
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, suavizacao);
    }
}
