import java.util.Properties

plugins {
    id("com.android.application")
    id("org.jetbrains.kotlin.android")
    id("org.jetbrains.kotlin.plugin.compose")
}

android {
    namespace = "com.lanerush.mobile"
    compileSdk = 35
    defaultConfig {
        applicationId = "com.lanerush.mobile"
        minSdk = 26
        targetSdk = 35
        versionCode = 6
        versionName = "2.0.0"
    }
    buildFeatures { compose = true }
    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }
    kotlinOptions { jvmTarget = "17" }
    packaging { resources.excludes += "/META-INF/{AL2.0,LGPL2.1}" }
    val uploadProperties = Properties().apply {
        load(rootProject.file("../lane_rush_android/android/key.properties").inputStream())
    }
    signingConfigs {
        create("release") {
            keyAlias = uploadProperties["keyAlias"] as String
            keyPassword = uploadProperties["keyPassword"] as String
            storeFile = rootProject.file("../lane_rush_android/android/app/upload-keystore.jks")
            storePassword = uploadProperties["storePassword"] as String
        }
    }
    buildTypes {
        release {
            signingConfig = signingConfigs.getByName("release")
            isMinifyEnabled = true
            isShrinkResources = true
            proguardFiles(getDefaultProguardFile("proguard-android-optimize.txt"), "proguard-rules.pro")
        }
    }
}

dependencies {
    implementation("androidx.activity:activity-compose:1.10.1")
    implementation(platform("androidx.compose:compose-bom:2025.06.01"))
    implementation("androidx.compose.ui:ui")
    implementation("androidx.compose.foundation:foundation")
    implementation("androidx.compose.material3:material3")
    implementation("io.github.sceneview:sceneview:4.0.0")
}
