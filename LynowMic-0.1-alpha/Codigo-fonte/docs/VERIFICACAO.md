# Verificação da entrega — 19/10/2026

## Concluído neste ambiente

- Compilação C# / WPF em Release para Windows x64 com .NET SDK 8.0.426.
- Publicação autocontida em arquivo executável único, com ícone próprio.
- Compilação Android `assembleDebug` com JDK 17, Gradle 8.9, AGP 8.7.3 e SDK 35.
- Quatro testes Android/JVM: pareamento local válido; rejeição de endereço/porta/pin inválidos; PCM little-endian com saturação; silêncio com preservação do enquadramento.
- Dez verificações do protocolo no servidor real via TCP/TLS em loopback: QR com pin/token; rejeição de certificado divergente; token incorreto; número JSON fora de faixa; sessão válida; reconstrução de PCM fragmentado; rejeição de comprimento inválido; reconexão após quadro malformado; recuperação após quadro truncado; cancelamento de handshake travado.
- SHA-256 da distribuição Gradle conferido e fixado no wrapper.

## Não verificado

- Execução da interface Windows em um Windows real.
- Execução da interface Android e leitura de QR Code por câmera física.
- Captura de microfone Android em aparelho real.
- Interoperabilidade completa Android físico → Windows físico → VB-CABLE → Discord.
- Qualidade perceptiva, latência ponta a ponta, consumo de bateria, tela bloqueada e comportamento de fabricantes diferentes.
- Assinatura de produção, instalador, atualização automática, lojas, driver próprio, USB e Bluetooth.

Os resultados confirmam compilação e invariantes do protocolo, não certificam uma versão pronta para produção. O pacote é uma alpha testável com código-fonte integral. Use `TESTE-NOS-DISPOSITIVOS.md` como próxima etapa.
