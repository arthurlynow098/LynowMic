package com.lynow.mic

import android.content.Context
import android.graphics.Canvas
import android.graphics.Color
import android.graphics.Paint
import android.util.AttributeSet
import android.view.View
import kotlin.math.sqrt

class LevelView(context: Context, attrs: AttributeSet?) : View(context, attrs) {
    private val paint = Paint(Paint.ANTI_ALIAS_FLAG)
    private val history = FloatArray(36)
    fun push(value: Float) { System.arraycopy(history, 1, history, 0, history.size - 1); history[history.lastIndex] = value; invalidate() }
    override fun onDraw(canvas: Canvas) {
        super.onDraw(canvas)
        val step = width / history.size.toFloat()
        for (i in history.indices) {
            val size = maxOf(5f * resources.displayMetrics.density, sqrt(history[i].coerceIn(0f, 1f)) * height * .85f)
            paint.color = Color.parseColor(if (history[i] >= .98f) "#FF4D6D" else if (history[i] > .001f) "#9D7AFF" else "#252238")
            canvas.drawRoundRect(i * step + 2f, (height - size) / 2, (i + 1) * step - 2f, (height + size) / 2, 4f, 4f, paint)
        }
    }
}
