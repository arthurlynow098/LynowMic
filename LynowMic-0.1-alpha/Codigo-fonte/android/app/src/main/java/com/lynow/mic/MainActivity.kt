package com.lynow.mic

import android.Manifest
import android.content.Intent
import android.content.pm.PackageManager
import android.media.audiofx.NoiseSuppressor
import android.os.*
import android.text.InputType
import android.widget.*
import androidx.activity.result.contract.ActivityResultContracts
import androidx.appcompat.app.AlertDialog
import androidx.appcompat.app.AppCompatActivity
import androidx.appcompat.widget.SwitchCompat
import androidx.core.content.ContextCompat
import androidx.core.view.ViewCompat
import androidx.core.view.WindowInsetsCompat
import com.journeyapps.barcodescanner.ScanContract
import com.journeyapps.barcodescanner.ScanOptions

class MainActivity : AppCompatActivity() {
    private var pendingPairing: String? = null
    private var launching = false
    private var launchedAt = 0L
    private val handler = Handler(Looper.getMainLooper())
    private lateinit var connect: Button
    private lateinit var manual: Button
    private lateinit var mute: Button
    private lateinit var noise: SwitchCompat
    private val permissions = registerForActivityResult(ActivityResultContracts.RequestMultiplePermissions()) {
        if (ContextCompat.checkSelfPermission(this, Manifest.permission.RECORD_AUDIO) == PackageManager.PERMISSION_GRANTED) startMic()
        else { launching = false; pendingPairing = null; message("O acesso ao microfone é necessário para transmitir sua voz.") }
    }
    private val scanner = registerForActivityResult(ScanContract()) { result ->
        result.contents?.let { acceptPairing(it) }
    }
    private val refresh = object : Runnable {
        override fun run() {
            if (MicService.running || SystemClock.elapsedRealtime() - launchedAt > 6000) launching = false
            val active = MicService.running || launching
            connect.text = if (active) "Desconectar" else "Escanear QR Code"
            manual.isEnabled = !active; mute.isEnabled = MicService.connected
            noise.isEnabled = !active && NoiseSuppressor.isAvailable()
            mute.text = if (MicService.muted) "Ativar microfone" else "Silenciar microfone"
            findViewById<TextView>(R.id.badge).text = when { MicService.connected && MicService.muted -> "●  MICROFONE SILENCIADO"; MicService.connected -> "●  TRANSMITINDO"; active -> "○  CONECTANDO"; else -> "○  PRONTO PARA CONECTAR" }
            findViewById<TextView>(R.id.connectionTitle).text = if (MicService.connected) "Sua voz está conectada" else "Encontre seu computador"
            findViewById<TextView>(R.id.status).text = MicService.status
            findViewById<LevelView>(R.id.meter).push(if (MicService.connected) MicService.peak else 0f)
            val seconds = if (MicService.connected) (SystemClock.elapsedRealtime() - MicService.startedAt) / 1000 else 0
            findViewById<TextView>(R.id.time).text = String.format(java.util.Locale.ROOT, "%02d:%02d:%02d", seconds / 3600, seconds / 60 % 60, seconds % 60)
            handler.postDelayed(this, 80)
        }
    }
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState); setContentView(R.layout.activity_main)
        pendingPairing = savedInstanceState?.getString("pending")
        ViewCompat.setOnApplyWindowInsetsListener(findViewById(R.id.root)) { view, insets ->
            val bars = insets.getInsets(WindowInsetsCompat.Type.systemBars()); view.setPadding(bars.left, bars.top, bars.right, bars.bottom); insets
        }
        connect = findViewById(R.id.connect); manual = findViewById(R.id.manual); mute = findViewById(R.id.mute); noise = findViewById(R.id.noise)
        if (!NoiseSuppressor.isAvailable()) noise.text = "Redução de ruído indisponível"
        connect.setOnClickListener {
            if (MicService.running || launching) { launching = false; stopService(Intent(this, MicService::class.java)); MicService.status = "Conexão encerrada." }
            else scanner.launch(ScanOptions().setDesiredBarcodeFormats(ScanOptions.QR_CODE).setPrompt("Escaneie o QR Code no Lynow Mic para Windows").setBeepEnabled(false).setOrientationLocked(false))
        }
        manual.setOnClickListener {
            val input = EditText(this).apply { hint = "Cole o código completo do computador"; inputType = InputType.TYPE_CLASS_TEXT or InputType.TYPE_TEXT_FLAG_MULTI_LINE; maxLines = 5 }
            AlertDialog.Builder(this).setTitle("Código de conexão").setMessage("No Windows, clique em Copiar código de conexão. Cole aqui o texto completo.").setView(input)
                .setPositiveButton("Conectar") { _, _ -> acceptPairing(input.text.toString().trim()) }.setNegativeButton("Cancelar", null).show()
        }
        mute.setOnClickListener { MicService.muted = !MicService.muted }
        findViewById<SeekBar>(R.id.gain).apply {
            progress = (MicService.gain * 100).toInt()
            setOnSeekBarChangeListener(object : SeekBar.OnSeekBarChangeListener {
                override fun onProgressChanged(seekBar: SeekBar?, value: Int, fromUser: Boolean) { MicService.gain = value / 100f; findViewById<TextView>(R.id.gainLabel).text = "Ganho de entrada · $value%" }
                override fun onStartTrackingTouch(seekBar: SeekBar?) { }
                override fun onStopTrackingTouch(seekBar: SeekBar?) { }
            })
        }
    }
    private fun acceptPairing(text: String) {
        try { Pairing.parse(text) } catch (e: Exception) { message(e.message ?: "Código inválido."); return }
        pendingPairing = text
        val needed = mutableListOf<String>()
        if (ContextCompat.checkSelfPermission(this, Manifest.permission.RECORD_AUDIO) != PackageManager.PERMISSION_GRANTED) needed.add(Manifest.permission.RECORD_AUDIO)
        if (Build.VERSION.SDK_INT >= 33 && ContextCompat.checkSelfPermission(this, Manifest.permission.POST_NOTIFICATIONS) != PackageManager.PERMISSION_GRANTED) needed.add(Manifest.permission.POST_NOTIFICATIONS)
        if (needed.isEmpty()) startMic() else permissions.launch(needed.toTypedArray())
    }
    private fun startMic() {
        val code = pendingPairing ?: return
        launching = true; launchedAt = SystemClock.elapsedRealtime(); MicService.status = "Iniciando o microfone…"
        try { ContextCompat.startForegroundService(this, Intent(this, MicService::class.java).putExtra("pairing", code).putExtra("noise", noise.isChecked)) }
        catch (e: Exception) { launching = false; message("Não foi possível iniciar. Abra o app e tente novamente.") }
        pendingPairing = null
    }
    private fun message(text: String) { AlertDialog.Builder(this).setTitle("Lynow Mic").setMessage(text).setPositiveButton("Entendi", null).show() }
    override fun onResume() { super.onResume(); handler.post(refresh) }
    override fun onPause() { handler.removeCallbacks(refresh); super.onPause() }
    override fun onSaveInstanceState(outState: Bundle) { outState.putString("pending", pendingPairing); super.onSaveInstanceState(outState) }
}
