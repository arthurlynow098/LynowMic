package com.lynow.mic

import android.Manifest
import android.app.*
import android.content.Intent
import android.content.pm.PackageManager
import android.content.pm.ServiceInfo
import android.media.*
import android.media.audiofx.NoiseSuppressor
import android.os.*
import androidx.core.app.NotificationCompat
import androidx.core.content.ContextCompat
import org.json.JSONObject
import java.io.IOException
import java.net.InetSocketAddress
import java.net.Socket
import java.security.MessageDigest
import java.security.cert.CertificateException
import java.security.cert.X509Certificate
import java.util.concurrent.Executors
import java.util.concurrent.TimeUnit
import javax.net.ssl.*

class MicService : Service() {
    companion object {
        const val STOP = "com.lynow.mic.STOP"
        @Volatile var running = false
        @Volatile var connected = false
        @Volatile var muted = false
        @Volatile var gain = 1f
        @Volatile var peak = 0f
        @Volatile var startedAt = 0L
        @Volatile var status = "Abra o Lynow Mic no Windows e toque em Preparar conexão."
        @Volatile private var current: MicService? = null
    }
    private val worker = Executors.newSingleThreadExecutor()
    private val watchdog = Executors.newSingleThreadScheduledExecutor()
    @Volatile private var socket: Socket? = null
    @Volatile private var lastProgress = 0L
    @Volatile private var recorder: AudioRecord? = null
    private var wakeLock: PowerManager.WakeLock? = null
    @Volatile private var active = false

    override fun onCreate() { super.onCreate(); current = this }

    override fun onBind(intent: Intent?) = null
    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        if (intent?.action == STOP) { shutdown("Conexão encerrada."); return START_NOT_STICKY }
        if (running) return START_NOT_STICKY
        val raw = intent?.getStringExtra("pairing") ?: return START_NOT_STICKY
        val pairing = try { Pairing.parse(raw) } catch (e: Exception) { status = e.message ?: "Código inválido."; stopSelf(); return START_NOT_STICKY }
        if (ContextCompat.checkSelfPermission(this, Manifest.permission.RECORD_AUDIO) != PackageManager.PERMISSION_GRANTED) {
            status = "Autorize o microfone para conectar."; stopSelf(); return START_NOT_STICKY
        }
        val notifications = getSystemService(NotificationManager::class.java)
        notifications.createNotificationChannel(NotificationChannel("microphone", "Microfone ativo", NotificationManager.IMPORTANCE_LOW))
        try {
            if (Build.VERSION.SDK_INT >= 29) startForeground(1, notification("Conectando ao computador…"), ServiceInfo.FOREGROUND_SERVICE_TYPE_MICROPHONE)
            else startForeground(1, notification("Conectando ao computador…"))
        } catch (e: Exception) { status = "Abra o aplicativo e tente conectar novamente."; stopSelf(); return START_NOT_STICKY }
        active = true; running = true; connected = false; muted = false; startedAt = 0L
        wakeLock = (getSystemService(POWER_SERVICE) as PowerManager).newWakeLock(PowerManager.PARTIAL_WAKE_LOCK, "LynowMic:Streaming").apply { acquire() }
        watchdog.scheduleAtFixedRate({
            if (active && socket != null && SystemClock.elapsedRealtime() - lastProgress > 8000) {
                try { socket?.close() } catch (_: Exception) { }
            }
        }, 2, 2, TimeUnit.SECONDS)
        val noise = intent.getBooleanExtra("noise", false)
        worker.execute {
            var attempts = 0
            try {
                while (active) {
                    try {
                        status = if (attempts == 0) "Conectando com segurança…" else "Reconectando… tentativa $attempts de 5"
                        getSystemService(NotificationManager::class.java).notify(1, notification(status))
                        stream(pairing, noise) { attempts = 0 }
                    } catch (e: Exception) {
                        if (!active) break
                        if (generateSequence<Throwable>(e) { it.cause }.any { it is CertificateException } || e is SecurityException) {
                            status = "Pareamento não autorizado. Gere e escaneie um novo QR Code."; break
                        }
                        attempts++
                        if (attempts > 5) { status = "Não foi possível conectar. Confira a rede, o Firewall e o programa no PC."; break }
                        status = "Conexão perdida. Reconectando ($attempts/5)…"
                        Thread.sleep(1500)
                    } finally {
                        if (current === this) { connected = false; peak = 0f }
                        try { socket?.close() } catch (_: Exception) { }
                        socket = null
                    }
                }
            } catch (_: InterruptedException) { }
            finally { Handler(Looper.getMainLooper()).post { if (current === this && active) shutdown(status) } }
        }
        return START_NOT_STICKY
    }

    private fun stream(pairing: Pairing, noise: Boolean, onStable: () -> Unit) {
        val expectedPin = pairing.pin.chunked(2).map { it.toInt(16).toByte() }.toByteArray()
        val trust = object : X509TrustManager {
            override fun getAcceptedIssuers(): Array<X509Certificate> = emptyArray()
            override fun checkClientTrusted(chain: Array<X509Certificate>, authType: String) { throw CertificateException("Não é um servidor.") }
            override fun checkServerTrusted(chain: Array<X509Certificate>, authType: String) {
                if (chain.isEmpty() || !MessageDigest.isEqual(expectedPin, MessageDigest.getInstance("SHA-256").digest(chain[0].encoded)))
                    throw CertificateException("O certificado não corresponde ao QR Code.")
                // Session certificate is pinned exactly; no trust-all fallback or public CA required.
            }
        }
        val ssl = SSLContext.getInstance("TLS").apply { init(null, arrayOf(trust), null) }
        val connection = ssl.socketFactory.createSocket() as SSLSocket
        socket = connection; lastProgress = SystemClock.elapsedRealtime()
        connection.tcpNoDelay = true; connection.soTimeout = 6000
        connection.enabledProtocols = arrayOf("TLSv1.2")
        connection.connect(InetSocketAddress(pairing.host, pairing.port), 5000)
        connection.startHandshake()
        val output = connection.outputStream
        val hello = JSONObject().put("v", 1).put("token", pairing.token).put("name", Build.MODEL.take(60))
            .put("rate", Pcm.RATE).put("channels", 1).put("bits", 16).toString() + "\n"
        output.write(hello.toByteArray(Charsets.UTF_8)); output.flush()
        if (connection.inputStream.read() != 1) throw SecurityException("Pareamento recusado")
        if (!active) return
        val minimum = AudioRecord.getMinBufferSize(Pcm.RATE, AudioFormat.CHANNEL_IN_MONO, AudioFormat.ENCODING_PCM_16BIT)
        if (minimum <= 0) throw IOException("Microfone incompatível com 48 kHz.")
        val record = AudioRecord(MediaRecorder.AudioSource.MIC, Pcm.RATE, AudioFormat.CHANNEL_IN_MONO,
            AudioFormat.ENCODING_PCM_16BIT, maxOf(minimum, Pcm.BYTES * 4))
        recorder = record
        var suppressor: NoiseSuppressor? = null
        try {
            if (record.state != AudioRecord.STATE_INITIALIZED) throw IOException("Não foi possível abrir o microfone.")
            if (noise && NoiseSuppressor.isAvailable()) suppressor = NoiseSuppressor.create(record.audioSessionId)?.apply { enabled = true }
            record.startRecording()
            if (record.recordingState != AudioRecord.RECORDSTATE_RECORDING) throw IOException("Microfone indisponível.")
            connected = true; startedAt = SystemClock.elapsedRealtime()
            status = "Conectado a ${pairing.host}. Sua voz está sendo transmitida."
            getSystemService(NotificationManager::class.java).notify(1, notification("Transmitindo para ${pairing.host}"))
            val samples = ShortArray(Pcm.SAMPLES)
            val packet = ByteArray(Pcm.BYTES + 4)
            var count = 0
            while (active) {
                var offset = 0
                while (offset < samples.size && active) {
                    val read = record.read(samples, offset, samples.size - offset, AudioRecord.READ_BLOCKING)
                    if (read <= 0) throw IOException("A captura de áudio foi interrompida.")
                    offset += read
                }
                if (!active) break
                peak = Pcm.encode(samples, gain, muted, packet)
                output.write(packet)
                lastProgress = SystemClock.elapsedRealtime()
                count++
                if (count == 250) onStable() // Reset retry budget only after five stable seconds.
            }
        } finally {
            try { record.stop() } catch (_: Exception) { }
            suppressor?.release(); record.release(); recorder = null
        }
    }

    private fun notification(text: String): Notification {
        val open = PendingIntent.getActivity(this, 0, Intent(this, MainActivity::class.java), PendingIntent.FLAG_IMMUTABLE or PendingIntent.FLAG_UPDATE_CURRENT)
        val stop = PendingIntent.getService(this, 1, Intent(this, MicService::class.java).setAction(STOP), PendingIntent.FLAG_IMMUTABLE or PendingIntent.FLAG_UPDATE_CURRENT)
        return NotificationCompat.Builder(this, "microphone").setSmallIcon(R.drawable.ic_notification).setContentTitle("Lynow Mic")
            .setContentText(text).setContentIntent(open).setOngoing(true).setOnlyAlertOnce(true)
            .addAction(0, "Desconectar", stop).build()
    }
    private fun shutdown(message: String) {
        active = false
        if (current === this) { running = false; connected = false; peak = 0f; status = message }
        try { socket?.close() } catch (_: Exception) { }
        try { recorder?.stop() } catch (_: Exception) { }
        if (wakeLock?.isHeld == true) wakeLock?.release()
        stopForeground(STOP_FOREGROUND_REMOVE); stopSelf()
    }
    override fun onDestroy() {
        active = false
        if (current === this) { running = false; connected = false; peak = 0f; current = null }
        try { socket?.close() } catch (_: Exception) { }
        try { recorder?.stop() } catch (_: Exception) { }
        watchdog.shutdownNow(); worker.shutdownNow()
        if (wakeLock?.isHeld == true) wakeLock?.release()
        super.onDestroy()
    }
}
