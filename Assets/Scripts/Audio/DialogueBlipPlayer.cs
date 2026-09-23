using System.Threading;
using TMPro;
using UnityEngine;
using Yarn.Markup;
using Yarn.Unity;

/// <summary>
/// Blip de diálogo — um som curto a cada letra visível revelada pelo typewriter do
/// Yarn Spinner. Não mexe no LinePresenter nem no pacote: LinePresenter já expõe uma
/// lista serializada "Event Handlers" (campo eventHandlers, tipo ActionMarkupHandler)
/// feita exatamente para isto. Basta arrastar este componente para essa lista.
///
/// Usa um AudioSource próprio em vez do SFXManager compartilhado: uma fala típica
/// dispara isto a 60 vezes por segundo (lettersPerSecond), e o SFXManager loga toda
/// chamada de Play — o que inundaria o console e não serve a nenhum outro som do jogo.
/// </summary>
public class DialogueBlipPlayer : ActionMarkupHandler
{
    [Tooltip("Som tocado a cada letra visível da fala (espaços são pulados).")]
    public AudioClip blipClip;

    [Tooltip("Volume do blip (0 a 1).")]
    [Range(0f, 1f)]
    public float volume = 0.5f;

    [Tooltip("Variação aleatória de pitch por letra, para não soar igual toda vez.")]
    public Vector2 pitchRange = new Vector2(0.475f, 0.525f);

    private AudioSource audioSource;
    private string currentText = string.Empty;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
    }

    public override void OnPrepareForLine(MarkupParseResult line, TMP_Text text)
    {
        currentText = line.Text;
    }

    public override void OnLineDisplayBegin(MarkupParseResult line, TMP_Text text) { }

    public override YarnTask OnCharacterWillAppear(int currentCharacterIndex, MarkupParseResult line, CancellationToken cancellationToken)
    {
        // O GameObject dono deste handler pode morrer (fim de cena, cutscene) enquanto o
        // LinePresenter — que pode ser persistente — ainda guarda a referência na sua
        // lista de Event Handlers. Sem esta checagem, a chamada seguinte joga um
        // MissingReferenceException que aborta a linha inteira e corrompe o resto do diálogo.
        if (audioSource == null) return YarnTask.CompletedTask;

        char c = (currentCharacterIndex >= 0 && currentCharacterIndex < currentText.Length)
            ? currentText[currentCharacterIndex]
            : ' ';

        if (!char.IsWhiteSpace(c) && blipClip != null)
        {
            audioSource.pitch = Random.Range(pitchRange.x, pitchRange.y);
            audioSource.PlayOneShot(blipClip, volume);
        }

        return YarnTask.CompletedTask;
    }

    public override void OnLineDisplayComplete() { }

    public override void OnLineWillDismiss() { }
}
