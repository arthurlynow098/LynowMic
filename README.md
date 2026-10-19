<p align="center">
  <img src="logo_source.jpg" width="150" alt="Logo do Lynow Mic">
</p>

<h1 align="center">Lynow Mic</h1>

O Lynow Mic nasceu de uma necessidade simples: usar o microfone de um celular Android no Windows sem depender de cabo. O celular captura a voz, envia o áudio pela rede local e o aplicativo no computador cuida da conexão e da saída.

> **Os arquivos prontos para uso estão na página de [Releases](https://github.com/arthurlynow098/LynowMic/releases).**
> Lá você encontra o `LynowMic.exe` para Windows, o APK para Android e o arquivo SHA-256 para conferência do executável.

## O que é o projeto

O projeto é formado por duas partes que trabalham juntas:

- o aplicativo Android usa o microfone do celular e transmite o áudio;
- o aplicativo Windows recebe esse áudio e permite escolher a interface de rede e o dispositivo de saída.

O pareamento é feito por QR Code. Depois que os dois aparelhos estão na mesma rede, o computador prepara a conexão e o celular passa a funcionar como a fonte de áudio. A transmissão atual usa PCM mono em 48 kHz e 16 bits, com conexão TLS validada durante o pareamento.

## Aplicativo para Windows

A tela do computador concentra o que é necessário para preparar e acompanhar a conexão. Nela é possível selecionar a rede, definir a saída de áudio, gerar o QR Code de pareamento e observar o nível do sinal recebido.

![Tela principal do Lynow Mic no Windows](LynowMic-0.1-alpha/images/hero_screenshot.jpg)

## Aplicativo para Android

No celular, a proposta é manter o uso direto: conectar ao computador, iniciar o microfone e acompanhar o estado da transmissão. A imagem abaixo mostra a direção visual adotada para essa experiência.

![Interface do Lynow Mic para Android](LynowMic-0.1-alpha/images/android_apk_mockup.jpg)

## Monitoramento web

O projeto também explora uma interface web para acompanhar o áudio com mais espaço: espectro em tempo real, nível em dBFS, latência e informações da sessão. Esta imagem apresenta a direção visual dessa área do projeto.

![Painel web de monitoramento do Lynow Mic](LynowMic-0.1-alpha/images/web_app_mockup.jpg)

## Como testar

1. Abra a página de [Releases](https://github.com/arthurlynow098/LynowMic/releases).
2. Baixe o `LynowMic.exe` no computador e o APK no celular Android.
3. Instale o APK e mantenha os dois aparelhos na mesma rede local.
4. Abra o Lynow Mic no Windows, escolha a rede e a saída de áudio.
5. Prepare a conexão e leia o QR Code com o aplicativo do celular.

Esta é uma versão alpha. Ela está disponível para testes e desenvolvimento, mas ainda pode apresentar limitações dependendo da rede, do aparelho Android e do dispositivo de áudio usado no Windows.

## Código-fonte

O código dos aplicativos, os scripts de compilação e a documentação técnica estão em [`LynowMic-0.1-alpha/Codigo-fonte`](LynowMic-0.1-alpha/Codigo-fonte).

- [`windows`](LynowMic-0.1-alpha/Codigo-fonte/windows): aplicativo Windows em WPF e C#;
- [`android`](LynowMic-0.1-alpha/Codigo-fonte/android): aplicativo Android em Kotlin;
- [`docs`](LynowMic-0.1-alpha/Codigo-fonte/docs): protocolo, testes e verificações;
- [`scripts`](LynowMic-0.1-alpha/Codigo-fonte/scripts): rotinas de compilação e assinatura.

## Licença

O Lynow Mic é distribuído sob a licença MIT. As licenças dos componentes usados pelo projeto estão reunidas no diretório [`licenses`](LynowMic-0.1-alpha/Codigo-fonte/licenses).
