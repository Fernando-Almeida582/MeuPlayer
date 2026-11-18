# Meu Player 1.4.2

Widget de mídia para Windows 10 e 11. Controla a sessão de mídia ativa exposta ao sistema por aplicativos como Spotify e navegadores. A interface mostra faixa, artista e capa quando o aplicativo disponibiliza esses dados.

## Abrir

1. Extraia todo o ZIP em uma pasta.
2. Abra **MeuPlayer.exe** na pasta extraída.
3. Clique com o botão direito no widget ou no ícone da bandeja para alternar entre a barra de tarefas e o modo flutuante, mudar a disposição, escolher um dos onze temas ou fechar o player.

A compilação é para Windows x64 e usa o **.NET Desktop Runtime 10 x64** instalado no sistema. Se precisar do runtime, baixe-o em [Microsoft .NET 10](https://dotnet.microsoft.com/en-us/download/dotnet/10.0). O executável desta compilação não tem assinatura digital.

No modo Estender, cada monitor recebe uma cópia do widget no modo fixo. As cópias compartilham a mídia, as baterias, os temas e as preferências. Se a barra de tarefas estiver desativada em um monitor, o widget aparece próximo à borda inferior dele. No modo solto, há apenas uma janela. O widget fica inicialmente na barra de tarefas e mantém a posição à frente da barra ao trocar de aplicativo ou abrir o menu Iniciar com a tecla Win. Enquanto o menu Iniciar ou a Busca estiver em foco, ou se a barra estiver à frente do widget, ele recua temporariamente para a área logo acima da barra. Assim que o encaixe fica disponível, o widget volta sozinho. O menu Iniciar e a Busca não acionam a opção de ocultação em tela cheia. Para ocultá-lo em tela cheia no modo fixo, marque **Exibir → Ocultar em tela cheia (fixo)**. Em **Posição → Layout vertical (solto)**, o widget vira um painel estreito com controles, capa, faixa e baterias empilhados. No modo flutuante, arraste pelo fundo, pela capa ou pelo título. Ao arrastar a capa ou o título, mantenha o botão esquerdo pressionado e mova o ponteiro. O ícone identifica o tipo de dispositivo Bluetooth e a porcentagem aparece abaixo. Passe o mouse sobre o ícone para ver o nome do dispositivo. Um traço indica que o Windows ainda não disponibilizou a porcentagem. A bateria é atualizada a partir dos eventos de dispositivo do Windows.

Os botões habilitados mudam de cor ao passar o ponteiro e durante o toque ou clique. Se você pausar pelo widget, o botão de reprodução fica destacado até a mídia voltar a tocar.

Em **Tema → Adaptativo**, o widget usa as cores da capa atual para escolher o fundo, a borda e os destaques. Quando a sessão não fornece uma capa, usa a paleta escura.

## Voltar para a mídia

Clique na capa ou no título para trazer a janela do aplicativo de origem para frente. Se a mídia estiver em um navegador compatível, o widget procura uma aba aberta com o mesmo título. Quando o título do anúncio não coincide com o do vídeo, o widget tenta localizar a aba do YouTube pelo indicador de reprodução de áudio. Se houver dúvida sobre a aba, ele traz a janela do navegador para frente. A busca usa os nomes acessíveis das abas abertas e acontece apenas ao clicar. Não instala extensão, não grava os nomes das abas e não abre links nem faz buscas na internet.

## Escolher a posição da mídia

Passe o mouse sobre a capa ou o título para abrir a barra de progresso. Clique ou arraste na barra para pular para a posição desejada. A barra aparece quando o aplicativo de origem permite mudar a posição pelo Windows. O clique na própria capa ou no título continua abrindo o aplicativo ou a aba de origem.

## Letra da faixa

Marque **Exibir → Exibir letra da faixa** para mostrar a linha sincronizada no lugar do título. O artista, a capa, os controles e a barra de progresso continuam disponíveis. Quando não há letra sincronizada ou o aplicativo de origem não informa a posição, o widget mostra o título normalmente. O mesmo trecho é compartilhado entre os monitores.

Com a opção habilitada, o widget procura a faixa no LRCLIB usando título, artista e, quando disponíveis, álbum e duração. Arquivos `.lrc` associados em versões anteriores continuam tendo prioridade. A busca começa quando a faixa fornece posição e artista. O temporizador das linhas só funciona quando há letra sincronizada e a mídia está tocando.

## Segurança e dados

O aplicativo usa APIs locais do Windows com os privilégios da conta atual. O código não inclui telemetria, atualizador nem hooks globais. Quando **Exibir letra da faixa** está ativa, a busca envia ao LRCLIB via HTTPS somente os dados da faixa usados na consulta. O aplicativo não acessa suas contas, credenciais ou cookies do Spotify ou do YouTube. **Iniciar com o Windows** é opcional e grava uma entrada de inicialização para a conta atual apenas quando você escolhe a opção no menu. Para mover o programa com a opção ativa, desative-a, mova a pasta e ative-a novamente a partir do novo local. O programa consulta o alinhamento da barra de tarefas no Registro em modo somente leitura. As preferências de aparência e posição são gravadas quando você muda uma opção em **%LocalAppData%\MeuPlayer\preferencias.json**.

A leitura de bateria usa os níveis que o Windows disponibiliza para dispositivos Bluetooth conectados. Esta versão não inclui espectro de áudio ou comunicação com controles e receptores por protocolos proprietários.

## Revisar, personalizar e compilar

Extraia o ZIP do código-fonte. O layout está em **PlayerWindow.xaml** e o comportamento em **PlayerWindow.xaml.cs**. O acesso à bateria fica em **PeripheralBattery.cs**. O projeto usa WPF e os componentes da Microsoft do SDK do Windows, sem referências a pacotes de terceiros.

Com o .NET 10 SDK para Windows, abra um terminal na pasta que contém **MeuPlayer.csproj** e execute:

```powershell
dotnet publish .\MeuPlayer.csproj -c Release --self-contained false -o .\publicacao
```

O executável e os arquivos de suporte ficam juntos na pasta **publicacao**.
