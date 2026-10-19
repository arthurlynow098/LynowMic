package com.lynow.mic

import org.json.JSONObject

data class Pairing(val host: String, val port: Int, val pin: String, val token: String) {
    companion object {
        fun parse(text: String): Pairing {
            require(text.length <= 2048) { "Código muito longo." }
            val j = JSONObject(text)
            require(j.getString("app") == "lynow-mic" && j.getInt("v") == 1) { "Este QR Code não é do Lynow Mic." }
            val host = j.getString("host")
            val octets = host.split('.').map { it.toIntOrNull() ?: -1 }
            require(octets.size == 4 && octets.all { it in 0..255 } &&
                (octets[0] == 10 || (octets[0] == 172 && octets[1] in 16..31) ||
                 (octets[0] == 192 && octets[1] == 168) || (octets[0] == 169 && octets[1] == 254))) { "Use um endereço IPv4 da rede local." }
            val port = j.getInt("port")
            require(port in 1024..65535) { "Porta inválida." }
            val pin = j.getString("pin").uppercase()
            val token = j.getString("token").uppercase()
            require(pin.matches(Regex("[0-9A-F]{64}")) && token.matches(Regex("[0-9A-F]{64}"))) { "Código incompleto. Escaneie novamente." }
            return Pairing(host, port, pin, token)
        }
    }
}
