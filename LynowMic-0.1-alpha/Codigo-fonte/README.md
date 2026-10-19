# Lynow Mic — Android + Windows

Versão **0.1.0 alpha**, criada para Arthur. Aplicativos nativos: Android em Kotlin e Windows em C#/WPF. Não é um site.

O celular transmite áudio para o computador pela rede local. O programa encaminha esse áudio para uma saída escolhida. Com um cabo de áudio virtual instalado separadamente, essa saída vira uma entrada de microfone no Discord, OBS ou em jogos.

## Comece aqui

Leia **LEIA-ME-PRIMEIRO.html**, na raiz do pacote de entrega. Esse guia abre em qualquer navegador e serve apenas como manual; os aplicativos são nativos.

1. Windows: execute `Windows/LynowMic.exe` do pacote de entrega.
2. Android: transfira e instale `Android/LynowMic-android-alpha.apk`.
3. Para usar como microfone de jogos, instale o [VB-CABLE oficial](https://vb-audio.com/Cable/) separadamente e reinicie o Windows.
4. No Lynow Mic para Windows, selecione sua rede local e **CABLE Input** em “Enviar áudio para”. Clique **Preparar conexão**.
5. Android e PC devem estar na mesma rede. No celular, toque **Escanear QR Code**, escaneie a tela e conceda acesso à câmera e ao microfone.
6. No Discord/jogo/OBS, escolha **CABLE Output** como dispositivo de entrada.

Os nomes são contraintuitivos: o Lynow envia som para **CABLE Input** (reprodução); o outro programa lê **CABLE Output** (gravação). Não selecione CABLE Output como saída do Lynow.

Para testar sem driver, escolha seus **fones** como saída no Lynow. Isso reproduz a voz para você, mas não cria um microfone do sistema. Evite alto-falantes, pois podem gerar eco ou microfonia.

## O que está implementado

- Aplicativos nativos com tema escuro, detalhes verde-menta e interfaces em português.
- Pareamento por QR Code e alternativa de colar o código completo.
- TLS 1.2 com pinagem do certificado da sessão e token aleatório de 256 bits.
- Áudio PCM 48 kHz, 16-bit, mono, em quadros de 20 ms, sem compressão com perdas.
- Controle de ganho e silenciar em ambos os aplicativos.
- Nível de áudio real, duração da conexão e buffer de reprodução no PC.
- Redução de ruído nativa do Android, quando o aparelho oferece esse efeito.
- Serviço Android em primeiro plano, notificação com botão Desconectar e bloqueio parcial de suspensão durante uso.
- Reconexão automática: até cinco tentativas; contador reinicia após cinco segundos estáveis.
- Cancelamento da sessão, timeouts de rede, rejeição de códigos e quadros inválidos.
- Um celular por sessão. Reiniciar a conexão no Windows invalida o QR Code anterior.
- Sem cadastro, anúncios, telemetria, assinatura ou limite artificial de tempo.

## Limites desta alpha

**Não é uma equivalência completa ao WO Mic.** Ainda não inclui USB direto, Bluetooth, driver próprio, instalador Windows, publicação em lojas, atualização automática ou cancelamento de eco próprio. Usa Wi-Fi/Ethernet na mesma rede IPv4 privada; não usa nuvem nem depende de internet depois de instalado. Consumo de áudio bruto: aproximadamente 768 kbit/s, além do protocolo.

O driver virtual de terceiros tem licença própria e não está incluído. O VB-CABLE é donationware; respeite as condições do fornecedor. Sem esse componente, o programa apenas reproduz o áudio em uma saída existente.

O executável Windows não tem assinatura Authenticode; o Android fornecido é um APK de teste assinado com chave debug, não um release de loja. Não desative o antivírus nem o Play Protect. Se houver bloqueio, compile a partir do código ou investigue o alerta antes de continuar.

Os testes automatizados verificam o protocolo e a conversão de áudio; **não substituem um teste físico Android → Windows → Discord**. Não foi possível executar a interface WPF nem validar áudio, latência, câmera, bateria, roteamento VB-CABLE e comportamento com tela bloqueada nos seus dispositivos. O buffer mostrado no PC **não é a latência ponta a ponta**. Não há promessa de latência zero nem de qualidade superior à capacidade do microfone do celular.

Requisitos previstos: Windows 10 x64 com TLS 1.2 e saída WASAPI; Android 8.0 (API 26) ou superior com microfone. Para melhor rede, prefira Wi-Fi de 5 GHz e PC por Ethernet. Esses requisitos não representam uma matriz de hardware certificada.

## Desenvolvimento

### Windows

Instale o [SDK .NET 8](https://dotnet.microsoft.com/download/dotnet/8.0). Na raiz do código:

```powershell
dotnet build windows/LynowMic.Windows/LynowMic.Windows.csproj -c Release
dotnet run --project tests/ProtocolTests -c Release
./scripts/build-windows.ps1
```

A publicação gera um executável Windows x64 autocontido em `releases/windows`; o usuário final não precisa instalar .NET. Para outra arquitetura, adapte `-r`; não foi validada nesta entrega.

### Android

Abra a pasta `android` no Android Studio. Use JDK 17, SDK Platform 35, Build Tools 35/34 e Gradle 8.9 (wrapper incluído). As versões de dependências estão fixadas. O Gradle requer internet na primeira compilação.

```powershell
cd android
./gradlew.bat :app:assembleDebug :app:testDebugUnitTest
```

O APK estará em `android/app/build/outputs/apk/debug/app-debug.apk`. Para distribuir publicamente, gere um APK/AAB de release com **sua própria chave**, aumente a versão e preserve a chave em local seguro. Não reutilize uma chave debug como identidade de produção. Uma recompilação com outra chave pode exigir desinstalar a alpha anterior.

## Organização

| Pasta | Conteúdo |
|---|---|
| `android` | Captura, interface e transmissão TLS nativas |
| `windows/LynowMic.Windows` | Interface WPF, servidor TLS e saída WASAPI |
| `tests/ProtocolTests` | Testes de integração reais em loopback, sem hardware de áudio |
| `docs` | Protocolo, checklist físico e registros de verificação |
| `scripts` | Comandos de compilação para Windows |

## Privacidade e segurança

O áudio vai diretamente do celular ao IP local selecionado. Não é gravado em arquivo. O QR Code contém uma credencial temporária de acesso à sessão; não publique capturas dele. O certificado é gerado por sessão, não instalado como autoridade no sistema. A pinagem permite autenticar esse certificado local sem depender de uma CA pública. O programa escuta apenas na interface IPv4 local selecionada, porta TCP 48765. Permita a aplicação **somente em redes privadas** no Firewall; não desative o Firewall e não encaminhe portas no roteador.

O protocolo aceita um cliente por vez; um cliente local malicioso pode atrasar a aceitação por até o timeout do handshake. Não é um serviço para exposição à internet. Auditoria de segurança e testes de carga não foram feitos.

## Referências técnicas

- [Serviços de microfone Android](https://developer.android.com/develop/background-work/services/fgs/service-types)
- [Restrições de início em segundo plano](https://developer.android.com/develop/background-work/services/fgs/restrictions-bg-start)
- [Android Gradle Plugin 8.7](https://developer.android.com/build/releases/agp-8-7-0-release-notes)
- [NAudio](https://github.com/naudio/NAudio) — versão utilizada: 2.2.1
- [ZXing Android Embedded](https://github.com/journeyapps/zxing-android-embedded) — versão utilizada: 4.3.0
- [QRCoder](https://github.com/codebude/QRCoder) — versão utilizada: 1.6.0
- [VB-CABLE](https://vb-audio.com/Cable/)

Veja `THIRD-PARTY-NOTICES.md` para dependências e licenças.
