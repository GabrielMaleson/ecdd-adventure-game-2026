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

    [Tooltip("Variação aleatória de pitch por letra, para não soar igual toda vez.")]
    public Vector2 pitchRange = new Vector2(0.95f, 1.05f);

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
        char c = (currentCharacterIndex >= 0 && currentCharacterIndex < currentText.Length)
            ? currentText[currentCharacterIndex]
            : ' ';

        if (!char.IsWhiteSpace(c) && blipClip != null)
        {
            audioSource.pitch = Random.Range(pitchRange.x, pitchRange.y);
            audioSource.PlayOneShot(blipClip);
        }

        return YarnTask.CompletedTask;
    }

    public override void OnLineDisplayComplete() { }

    public override void OnLineWillDismiss() { }
}
