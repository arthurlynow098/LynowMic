package com.lynow.mic

import org.junit.Assert.*
import org.junit.Test

class ProtocolTest {
    private fun code(host: String = "192.168.1.4", port: Int = 48765, pin: String = "A".repeat(64)) =
        """{"app":"lynow-mic","v":1,"host":"$host","port":$port,"pin":"$pin","token":"${"B".repeat(64)}"}"""
    @Test fun acceptsLocalPairing() { assertEquals("192.168.1.4", Pairing.parse(code()).host); assertEquals(48765, Pairing.parse(code()).port) }
    @Test fun rejectsNonLocalOrMalformedEndpoints() {
        listOf("8.8.8.8", "127.0.0.1", "example.com", "192.168.1.999", "172.32.0.1", "10.1.2").forEach { host ->
            assertThrows(IllegalArgumentException::class.java) { Pairing.parse(code(host)) }
        }
        assertThrows(IllegalArgumentException::class.java) { Pairing.parse(code(port = 0)) }
        assertThrows(IllegalArgumentException::class.java) { Pairing.parse(code(pin = "bad")) }
    }
    @Test fun encodesLittleEndianAndClips() {
        val samples = ShortArray(Pcm.SAMPLES); samples[0] = 20000; samples[1] = -20000; samples[2] = 258
        val bytes = ByteArray(Pcm.BYTES + 4)
        assertEquals(1f, Pcm.encode(samples, 2f, false, bytes), 0f)
        assertArrayEquals(byteArrayOf(0x80.toByte(), 7, 0, 0, 0xff.toByte(), 0x7f, 0, 0x80.toByte(), 4, 2), bytes.take(10).toByteArray())
    }
    @Test fun muteSendsSilenceButKeepsFrameHeader() {
        val bytes = ByteArray(Pcm.BYTES + 4)
        assertEquals(0f, Pcm.encode(ShortArray(Pcm.SAMPLES) { 10000 }, 2f, true, bytes), 0f)
        assertTrue(bytes.drop(4).all { it == 0.toByte() }); assertEquals(7, bytes[1].toInt())
    }
}
