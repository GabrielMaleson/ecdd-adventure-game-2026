using System.Threading;
using TMPro;
using UnityEngine;
using Yarn.Markup;
using Yarn.Unity;

// Cede UM quadro no comeco de cada fala. Nada mais.
//
// ------------------------------------------------------------------ o bug que isto conserta
//
// Uma fala cujo texto nao tem NENHUMA palavra — "!", "...", "…" — passa pelo typewriter sem
// nunca esperar: o laco de tempo e pulado, e todo handler devolve tarefa ja completa. O
// resultado e que RunTypewriter termina de forma SINCRONA, dentro da propria chamada de
// RunLineAsync do LinePresenter.
//
// Isso importa por causa da ordem em que o DialogueRunner avisa os apresentadores:
//
//   1. LinePresenter.RunLineAsync   -> typewriter roda inteiro AQUI e chama
//                                      OnLineDisplayComplete(), que poe o LineAdvancer em
//                                      "LineWaiting" (o estado em que apertar avanca)
//   2. LineAdvancer.RunLineAsync    -> chama ResetLineTracking(), que devolve o estado para
//                                      "LineBegan"
//
// E OnLineDisplayComplete() nao acontece de novo, porque a fala ja foi exibida. O estado
// fica preso em "LineBegan" para sempre, e nesse estado apertar so pede "corre com essa
// fala" — nunca "proxima fala". A fala fica na tela e nao sai mais.
//
// Numa fala normal o typewriter espera pelo menos um quadro, entao OnLineDisplayComplete()
// cai DEPOIS dos avisos e o estado final e o certo. Por isso o travamento so aparecia em
// falas de pontuacao pura.
//
// ------------------------------------------------------------------ a correcao
//
// Um unico quadro cedido no primeiro caractere basta: com ele, RunTypewriter nunca mais
// termina de forma sincrona, e OnLineDisplayComplete() sempre cai depois de todos os
// RunLineAsync. Um quadro nao e perceptivel e nao mexe na velocidade do typewriter.
//
// COMO MONTAR: arraste este componente para a lista Event Handlers do Line Presenter.
public class YieldOncePerLine : ActionMarkupHandler
{
    public override void OnPrepareForLine(MarkupParseResult line, TMP_Text text) { }

    public override void OnLineDisplayBegin(MarkupParseResult line, TMP_Text text) { }

    public override async YarnTask OnCharacterWillAppear(int currentCharacterIndex,
                                                         MarkupParseResult line,
                                                         CancellationToken cancellationToken)
    {
        // So no primeiro caractere: um quadro por FALA, e nao um por letra — um por letra
        // brigaria com o Letters Per Second do proprio typewriter.
        if (currentCharacterIndex == 0)
            await YarnTask.Yield();
    }

    public override void OnLineDisplayComplete() { }

    public override void OnLineWillDismiss() { }
}
