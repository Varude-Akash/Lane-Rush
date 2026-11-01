package com.lanerush.mobile

import android.content.pm.ActivityInfo
import android.media.AudioAttributes
import android.media.AudioFormat
import android.media.AudioManager
import android.media.AudioTrack
import android.media.MediaPlayer
import android.media.ToneGenerator
import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.BackHandler
import androidx.activity.compose.setContent
import androidx.compose.foundation.background
import androidx.compose.foundation.gestures.detectDragGestures
import androidx.compose.foundation.gestures.detectTapGestures
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import io.github.sceneview.SceneView
import com.google.android.filament.ColorGrading
import com.google.android.filament.ToneMapper
import com.google.android.filament.View
import io.github.sceneview.math.Color as SceneColor
import io.github.sceneview.math.Direction
import io.github.sceneview.math.Position
import io.github.sceneview.math.Rotation
import io.github.sceneview.math.Size
import io.github.sceneview.node.CubeNode
import io.github.sceneview.node.ModelNode
import io.github.sceneview.rememberCameraNode
import io.github.sceneview.rememberEngine
import io.github.sceneview.rememberMainLightNode
import io.github.sceneview.rememberMaterialLoader
import io.github.sceneview.rememberModelInstance
import io.github.sceneview.rememberModelLoader
import io.github.sceneview.rememberView
import kotlin.math.abs
import kotlin.math.max
import kotlin.math.min
import kotlin.random.Random

class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        requestedOrientation = ActivityInfo.SCREEN_ORIENTATION_USER_LANDSCAPE
        super.onCreate(savedInstanceState)
        setContent { LaneRushGame() }
    }
}

private data class Traffic(var x: Float, var z: Float, var pace: Float)

private val trafficModels = listOf("traffic-normal-car-1190.glb", "traffic-suv-1402.glb", "traffic-taxi-1413.glb", "traffic-sports-car-1370.glb", "traffic-normal-car-1191.glb", "traffic-sports-car-1371.glb")
private val treeModels = listOf("tree-palm-1203.glb", "tree-common-836.glb", "tree-willow-1481.glb", "tree-willow-1483.glb", "tree-common-835.glb", "tree-common-838.glb", "tree-blendertimer-11.glb", "tree-common-839.glb", "tree-common-837.glb")
private val buildingModels = listOf("building-747.glb", "building-big-745.glb", "building-large-741.glb", "building-large-743.glb", "building-small-742.glb", "building-small-746.glb")

@Composable
private fun LaneRushGame() {
    val context = LocalContext.current
    val engine = rememberEngine()
    val loader = rememberModelLoader(engine)
    val materials = rememberMaterialLoader(engine)
    val camera = rememberCameraNode(engine).apply {
        position = Position(0f, 3.5f, 9.5f)
        lookAt(Position(0f, .9f, -14f))
    }
    val view = rememberView(engine).apply {
        // Keep the mobile render path cheap.  SSAO, bloom and real-time shadows were
        // the largest frame-time spikes on mid-range devices.
        antiAliasing = View.AntiAliasing.FXAA
        ambientOcclusion = View.AmbientOcclusion.NONE
        isPostProcessingEnabled = false
        setShadowingEnabled(false)
        fogOptions = View.FogOptions().apply {
            enabled = true
            distance = 44f
            density = .018f
            maximumOpacity = .82f
            height = 0f
            heightFalloff = .15f
            color = floatArrayOf(.42f, .58f, .68f)
        }
    }
    val colorGrading = remember(engine) {
        ColorGrading.Builder().toneMapper(ToneMapper.ACES()).exposure(.35f).contrast(1.08f).saturation(1.04f).build(engine)
    }
    DisposableEffect(colorGrading) {
        view.colorGrading = colorGrading
        onDispose { engine.destroyColorGrading(colorGrading) }
    }
    val sun = rememberMainLightNode(engine) {
        color = SceneColor(1f, .91f, .78f, 1f)
        intensity = 105_000f
        lightDirection = Direction(-.35f, -.8f, -.45f)
        isShadowCaster = true
    }
    val asphalt = remember { materials.createColorInstance(SceneColor(.065f, .075f, .085f, 1f), roughness = .88f) }
    val paint = remember { materials.createColorInstance(SceneColor(.96f, .95f, .79f, 1f), roughness = .55f) }
    val grass = remember { materials.createColorInstance(SceneColor(.075f, .25f, .105f, 1f), roughness = .96f) }
    val grassAlt = remember { materials.createColorInstance(SceneColor(.11f, .34f, .12f, 1f), roughness = .96f) }
    val sky = remember { materials.createColorInstance(SceneColor(.24f, .52f, .72f, 1f), roughness = 1f) }
    val curb = remember { materials.createColorInstance(SceneColor(.58f, .62f, .62f, 1f), roughness = .75f) }

    val playerAsset = rememberModelInstance(loader, "models/traffic-sports-car-1370.glb")
    val trafficAssets = trafficModels.map { rememberModelInstance(loader, "models/$it") }
    val treeAssets = treeModels.map { rememberModelInstance(loader, "models/$it") }
    val buildingAssets = buildingModels.map { rememberModelInstance(loader, "models/$it") }
    val assetsReady = playerAsset != null && trafficAssets.all { it != null } && treeAssets.all { it != null } && buildingAssets.all { it != null }
    val player = remember(playerAsset) { playerAsset?.let { ModelNode(it, scaleToUnits = 6.2f, centerOrigin = Position(0f, -1f, 0f)).apply { rotation = Rotation(y = 180f); setCulling(false) } } }
    val cars = trafficAssets.map { asset -> remember(asset) { asset?.let { ModelNode(it, scaleToUnits = 3.8f, centerOrigin = Position(0f, -1f, 0f)).apply { rotation = Rotation(y = 180f) } } } }
    val trees = treeAssets.mapIndexed { i, asset -> remember(asset) { asset?.let { ModelNode(it, scaleToUnits = if (i == 6) 10f else 8.5f, centerOrigin = Position(0f, -1f, 0f)) } } }
    val buildings = buildingAssets.map { asset -> remember(asset) { asset?.let { ModelNode(it, scaleToUnits = 13f, centerOrigin = Position(0f, -1f, 0f)) } } }

    val roads = remember { mutableListOf<CubeNode>() }
    val lines = remember { mutableListOf<CubeNode>() }
    val curbs = remember { mutableListOf<CubeNode>() }
    val grounds = remember { mutableListOf<CubeNode>() }
    val traffic = remember { MutableList(cars.size) { i -> Traffic(listOf(-5f, 0f, 5f)[i % 3], -52f - i * 31f, .3f + (i % 3) * .13f) } }
    var playerX by remember { mutableFloatStateOf(0f) }
    var targetX by remember { mutableFloatStateOf(0f) }
    var metres by remember { mutableIntStateOf(0) }
    var best by remember { mutableIntStateOf(0) }
    var speed by remember { mutableIntStateOf(145) }
    var nitro by remember { mutableFloatStateOf(1f) }
    var boosting by remember { mutableStateOf(false) }
    var paused by remember { mutableStateOf(false) }
    var crashed by remember { mutableStateOf(false) }
    var run by remember { mutableFloatStateOf(0f) }
    var lastFrame by remember { mutableLongStateOf(0L) }
    var hudTimer by remember { mutableFloatStateOf(0f) }
    var audioTimer by remember { mutableFloatStateOf(0f) }

    val engineAudio = remember { MediaPlayer() }
    val horn = remember { ToneGenerator(AudioManager.STREAM_MUSIC, 100) }
    DisposableEffect(Unit) {
        runCatching {
            context.assets.openFd("audio/engine-loop.wav").use { engineAudio.setDataSource(it.fileDescriptor, it.startOffset, it.length) }
            engineAudio.isLooping = true
            engineAudio.setVolume(.85f, .85f)
            engineAudio.prepare()
            engineAudio.start()
        }
        onDispose { engineAudio.release(); horn.release() }
    }
    fun restart() {
        playerX = 0f; targetX = 0f; run = 0f; metres = 0; speed = 145; nitro = 1f; crashed = false; paused = false; lastFrame = 0L
        traffic.forEachIndexed { i, car -> car.x = listOf(-5f, 0f, 5f)[i % 3]; car.z = -52f - i * 31f }
        engineAudio.start()
    }
    BackHandler { if (crashed) restart() else paused = !paused }

    val drivingInput = Modifier.pointerInput(crashed, paused) {
        detectDragGestures(onDragStart = { if (!crashed) targetX = (it.x / size.width * 13f - 6.5f).coerceIn(-6.1f, 6.1f) }) { change, drag ->
            change.consume(); if (!crashed && !paused) targetX = (targetX + drag.x / size.width * 14f).coerceIn(-6.1f, 6.1f)
        }
    }.pointerInput(crashed, paused) {
        detectTapGestures(onPress = { at ->
            if (!crashed && !paused) { targetX = (at.x / size.width * 13f - 6.5f).coerceIn(-6.1f, 6.1f); boosting = true; tryAwaitRelease(); boosting = false }
        })
    }
    Box(Modifier.fillMaxSize().background(Color(0xFF69A4C7))) {
        SceneView(modifier = Modifier.fillMaxSize().then(drivingInput), engine = engine, modelLoader = loader, materialLoader = materials, view = view, mainLightNode = sun, cameraNode = camera, cameraManipulator = null,
            onFrame = { time ->
                if (lastFrame == 0L) lastFrame = time
                val dt = min((time - lastFrame) / 1_000_000_000f, .05f); lastFrame = time
                if (!paused && !crashed && dt > 0f) {
                    val level = min(run / 4500f, 1f)
                    val boost = boosting && nitro > .01f
                    val velocity = 24f + level * 15f + if (boost) 13f else 0f
                    speed = (145f + level * 135f + if (boost) 55f else 0f).toInt()
                    audioTimer += dt
                    if (audioTimer >= .12f) {
                        engineAudio.playbackParams = engineAudio.playbackParams.setSpeed(.82f + level * .42f + if (boost) .14f else 0f)
                        engineAudio.setVolume(if (boost) 1f else .85f, if (boost) 1f else .85f)
                        audioTimer = 0f
                    }
                    nitro = if (boost) max(0f, nitro - dt * .24f) else min(1f, nitro + dt * .075f)
                    playerX += (targetX - playerX) * min(1f, dt * 8f)
                    player?.position = Position(playerX, 0f, -8f)
                    player?.rotation = Rotation(y = 180f, z = -(targetX - playerX) * 1.7f)
                    run += velocity * dt; hudTimer += dt
                    if (hudTimer > .08f) { metres = run.toInt(); best = max(best, metres); hudTimer = 0f }
                    fun moved(z: Float, span: Float): Float { var value = z + velocity * dt; if (value > 14f) value -= span; return value }
                    roads.forEach { it.position = Position(it.position.x, it.position.y, moved(it.position.z, 216f)) }
                    lines.forEach { it.position = Position(it.position.x, it.position.y, moved(it.position.z, 216f)) }
                    curbs.forEach { it.position = Position(it.position.x, it.position.y, moved(it.position.z, 216f)) }
                    grounds.forEach { it.position = Position(it.position.x, it.position.y, moved(it.position.z, 216f)) }
                    trees.forEach { it?.position = Position(it?.position?.x ?: 0f, 0f, moved(it?.position?.z ?: 0f, 225f)) }
                    buildings.forEach { it?.position = Position(it?.position?.x ?: 0f, 0f, moved(it?.position?.z ?: 0f, 234f)) }
                    cars.forEachIndexed { i, node ->
                        val state = traffic[i]; state.z += velocity * (1f - state.pace) * dt
                        if (state.z > 13f) { state.z = traffic.minOf { it.z } - 34f - Random.nextFloat() * 20f; state.x = listOf(-5f, 0f, 5f).random(); state.pace = .3f + Random.nextFloat() * .35f }
                        node?.position = Position(state.x, 0f, state.z)
                        if (state.z in -10.7f..-5.3f && abs(state.x - playerX) < 2.35f) { crashed = true; engineAudio.pause(); playCrash() }
                    }
                }
            }
        ) {
            CubeNode(size = Size(1200f, 500f, 2f), materialInstance = sky, position = Position(0f, 90f, -420f))
            repeat(18) { i ->
                val z = -i * 12f
                CubeNode(size = Size(16f, .1f, 12.1f), materialInstance = asphalt, position = Position(0f, -.09f, z), apply = { roads += this })
                val vergeMaterial = if (i % 2 == 0) grass else grassAlt
                CubeNode(size = Size(34f, .08f, 12.1f), materialInstance = vergeMaterial, position = Position(-25f, -.13f, z), apply = { grounds += this })
                CubeNode(size = Size(34f, .08f, 12.1f), materialInstance = vergeMaterial, position = Position(25f, -.13f, z), apply = { grounds += this })
                listOf(-2.67f, 2.67f).forEach { x -> CubeNode(size = Size(.14f, .035f, 4.4f), materialInstance = paint, position = Position(x, 0f, z), apply = { lines += this }) }
                listOf(-8.1f, 8.1f).forEach { x -> CubeNode(size = Size(.25f, .18f, 12.1f), materialInstance = curb, position = Position(x, 0f, z), apply = { curbs += this }) }
            }
            trees.forEachIndexed { i, node -> node?.apply { position = Position(if (i % 2 == 0) -11f - i % 3 * 1.6f else 11f + i % 3 * 1.6f, 0f, -18f - i * 25f); rotation = Rotation(y = i * 47f % 360f) } }
            buildings.forEachIndexed { i, node -> node?.apply { position = Position(if (i % 2 == 0) -20f else 20f, 0f, -34f - i * 39f); rotation = Rotation(y = if (i % 2 == 0) 90f else -90f) } }
            player?.let { NodeLifecycle(it) {} }
            trees.forEach { it?.let { node -> NodeLifecycle(node) {} } }
            buildings.forEach { it?.let { node -> NodeLifecycle(node) {} } }
            cars.forEach { it?.let { node -> NodeLifecycle(node) {} } }
            cars.forEachIndexed { i, node -> node?.position = Position(traffic[i].x, 0f, traffic[i].z) }
        }
        Hud(metres, speed, best, nitro, paused, crashed,
            onPause = { paused = !paused; if (paused) engineAudio.pause() else engineAudio.start() },
            onRestart = ::restart,
            onBoost = { boosting = it },
            onHorn = { horn.startTone(ToneGenerator.TONE_DTMF_9, 320) }
        )
        if (!assetsReady) {
            Box(Modifier.fillMaxSize().background(Color(0xFF07111D)), contentAlignment = Alignment.Center) {
                Column(horizontalAlignment = Alignment.CenterHorizontally) {
                    Text("LR", color = Color(0xFFCCFF22), fontSize = 42.sp, fontWeight = FontWeight.Black)
                    Spacer(Modifier.height(12.dp))
                    Text("LOADING THE ROAD", color = Color.White, fontSize = 15.sp, fontWeight = FontWeight.Bold, fontFamily = FontFamily.Monospace)
                }
            }
        }
    }
}

@Composable
private fun Hud(metres: Int, speed: Int, best: Int, nitro: Float, paused: Boolean, crashed: Boolean, onPause: () -> Unit, onRestart: () -> Unit, onBoost: (Boolean) -> Unit, onHorn: () -> Unit) {
    val panel = Color(0xD9142029)
    Column(Modifier.fillMaxSize().padding(16.dp), verticalArrangement = Arrangement.SpaceBetween) {
        Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween, verticalAlignment = Alignment.Top) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Box(Modifier.background(Color(0xFFCCFF22), RoundedCornerShape(3.dp)).padding(10.dp, 8.dp)) { Text("LR", color = Color(0xFF101820), fontWeight = FontWeight.Black) }
                Spacer(Modifier.width(10.dp)); Text("LANE RUSH", color = Color.White, fontSize = 17.sp, fontWeight = FontWeight.Black)
            }
            Row(Modifier.background(panel, RoundedCornerShape(4.dp)).padding(12.dp, 7.dp)) {
                Stat("DISTANCE", "%04d M".format(metres)); Stat("SPEED", "$speed KM/H", Color(0xFFCCFF22)); Stat("BEST RUN", "%04d M".format(best))
            }
        }
        Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween, verticalAlignment = Alignment.Bottom) {
            Button(onClick = onPause, colors = ButtonDefaults.buttonColors(containerColor = panel), shape = RoundedCornerShape(4.dp)) { Text(if (paused) "RESUME" else "PAUSE") }
            Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                Button(
                    onClick = {},
                    modifier = Modifier.pointerInput(paused, crashed) { detectTapGestures(onPress = { if (!paused && !crashed) { onBoost(true); tryAwaitRelease(); onBoost(false) } }) },
                    colors = ButtonDefaults.buttonColors(containerColor = Color(0xFFCCFF22)),
                    shape = RoundedCornerShape(4.dp)
                ) { Text("BOOST", color = Color.Black, fontWeight = FontWeight.Black) }
                Button(onClick = onHorn, enabled = !paused && !crashed, colors = ButtonDefaults.buttonColors(containerColor = panel), shape = RoundedCornerShape(4.dp)) { Text("HORN", fontWeight = FontWeight.Black) }
            }
            Column(horizontalAlignment = Alignment.End) {
                Text("NITRO", color = Color.White, fontSize = 10.sp, fontWeight = FontWeight.Black)
                LinearProgressIndicator(progress = { nitro }, modifier = Modifier.width(180.dp).height(7.dp), color = Color(0xFFCCFF22), trackColor = Color.White.copy(.28f))
            }
        }
    }
    if (paused || crashed) Box(Modifier.fillMaxSize().background(Color.Black.copy(.58f)), contentAlignment = Alignment.Center) {
        Column(Modifier.background(Color(0xF2142029), RoundedCornerShape(6.dp)).padding(32.dp), horizontalAlignment = Alignment.CenterHorizontally) {
            Text(if (crashed) "RUN ENDED" else "PAUSED", color = Color.White, fontWeight = FontWeight.Bold)
            Text(if (crashed) "$metres METRES" else "LANE RUSH", color = Color(0xFFCCFF22), fontSize = 34.sp, fontWeight = FontWeight.Black)
            Spacer(Modifier.height(18.dp)); Button(onClick = if (crashed) onRestart else onPause, colors = ButtonDefaults.buttonColors(containerColor = Color(0xFFCCFF22))) { Text(if (crashed) "RESTART ENGINE" else "RESUME", color = Color.Black, fontWeight = FontWeight.Black) }
        }
    }
}

private fun playCrash() {
    Thread {
        val rate = 16_000
        val count = (rate * .20f).toInt()
        val samples = ShortArray(count) { i ->
            val fade = 1f - i.toFloat() / count
            (((Random.nextFloat() * 2f - 1f) * fade * fade) * Short.MAX_VALUE * .7f).toInt().toShort()
        }
        val track = AudioTrack.Builder()
            .setAudioAttributes(AudioAttributes.Builder().setUsage(AudioAttributes.USAGE_GAME).setContentType(AudioAttributes.CONTENT_TYPE_SONIFICATION).build())
            .setAudioFormat(AudioFormat.Builder().setEncoding(AudioFormat.ENCODING_PCM_16BIT).setSampleRate(rate).setChannelMask(AudioFormat.CHANNEL_OUT_MONO).build())
            .setBufferSizeInBytes(samples.size * 2)
            .setTransferMode(AudioTrack.MODE_STATIC)
            .build()
        track.write(samples, 0, samples.size)
        track.play()
        Thread.sleep(240)
        track.release()
    }.start()
}

@Composable private fun Stat(label: String, value: String, color: Color = Color.White) = Column(Modifier.padding(horizontal = 12.dp)) {
    Text(label, color = Color(0xFF96A9B5), fontSize = 8.sp, fontFamily = FontFamily.Monospace)
    Text(value, color = color, fontSize = 18.sp, fontWeight = FontWeight.Black, fontFamily = FontFamily.Monospace)
}
