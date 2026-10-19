# Protocolo Lynow Mic 1

Transporte TCP/TLS 1.2, apenas IPv4 local. Servidor Windows em 48765. O Android conecta e valida SHA-256 dos bytes DER do certificado contra `pin` do QR. Não existe fallback trust-all. O QR é o canal inicial de confiança e deve ser lido diretamente na tela do PC.

QR (JSON UTF-8): `app="lynow-mic"`, `v=1`, `host` privado, `port`, `pin` hex de 64 caracteres, `token` hex de 64 caracteres. Pin e token são aleatórios/derivados da sessão, sem persistência. Nenhum segredo de exemplo é uma credencial utilizável.

Dentro do TLS, o Android envia uma linha JSON UTF-8 terminada em LF, no máximo 2048 bytes:

```json
{"v":1,"token":"<token do QR>","name":"modelo do aparelho","rate":48000,"channels":1,"bits":16}
```

O servidor valida token em tempo constante e formato exato; responde um byte `0x01`. Em falha, fecha a conexão. Após esse byte, cada quadro consiste em `Int32 little-endian` de valor **1920** seguido por exatamente **1920 bytes**, ou 960 amostras PCM16 signed little-endian, mono. Duração de quadro: 20 ms.

Mute conserva o envio de quadros com zeros. Ganho digital satura em [-32768, 32767]; não há AGC próprio ou DSP secreto. Ruído é tratado apenas pelo efeito opcional do Android. TLS fornece autenticação e integridade, TCP fornece ordenação. Por usar TCP, perdas de rede podem aumentar atraso; essa versão prioriza simplicidade e transporte autenticado.

Timeout inicial: 6 s; timeout de leitura de quadro: 5 s. Android: connect 5 s, leitura de handshake 6 s, watchdog de progresso 8 s. Reconexão limitada a cinco falhas consecutivas, com pausa de 1,5 s. Só cinco segundos estáveis zeram o orçamento de falhas.

Windows usa um buffer de até 240 ms; quando a fila excede 160 ms, limpa áudio antigo. WASAPI compartilhado solicita 60 ms. O driver pode adicionar buffering e conversão; **nenhum desses números equivale à latência total**.

Não há gravação, descoberta mDNS, internet relay, UDP, USB ou Bluetooth nesta versão. Não há criação automática de dispositivo virtual. O driver de cabo virtual é uma dependência externa, não uma parte do protocolo.
