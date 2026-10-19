package com.lynow.mic

import kotlin.math.abs

object Pcm {
    const val RATE = 48000
    const val SAMPLES = 960
    const val BYTES = SAMPLES * 2
    // A frame is a little-endian Int32 payload size, then 20 ms of PCM16 LE.
    fun encode(samples: ShortArray, gain: Float, muted: Boolean, packet: ByteArray): Float {
        require(samples.size == SAMPLES && packet.size == BYTES + 4)
        packet[0] = (BYTES and 255).toByte(); packet[1] = (BYTES shr 8).toByte()
        packet[2] = 0; packet[3] = 0
        var peak = 0
        for (i in samples.indices) {
            val value = if (muted) 0 else (samples[i] * gain).toInt().coerceIn(-32768, 32767)
            peak = maxOf(peak, abs(value))
            packet[4 + i * 2] = (value and 255).toByte()
            packet[5 + i * 2] = ((value shr 8) and 255).toByte()
        }
        return peak / 32768f
    }
}
