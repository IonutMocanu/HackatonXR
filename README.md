# 🌿 AgriSense AR

![Unity](https://img.shields.io/badge/Unity-100000?style=for-the-badge&logo=unity&logoColor=white)
![Meta Quest](https://img.shields.io/badge/Meta_Quest-045FCE?style=for-the-badge&logo=meta&logoColor=white)
![YOLO](https://img.shields.io/badge/YOLO-00FFFF?style=for-the-badge&logo=yolo&logoColor=black)
![C#](https://img.shields.io/badge/C%23-239120?style=for-the-badge&logo=c-sharp&logoColor=white)
![Python](https://img.shields.io/badge/Python-3776AB?style=for-the-badge&logo=python&logoColor=white)

**AgriSense AR** is an innovative augmented reality (AR) application developed for the Meta Quest ecosystem, designed to facilitate the rapid identification and treatment of plant diseases. 

This project was a finalist at the **XR Creativity Challenge** in Berlin.

## ✨ Key Features

*   **Real-Time Detection:** Utilizes the **YOLO26s** computer vision model, running locally in Unity via **ONNX Runtime**, to instantly identify plant diseases directly from the headset's visual feed (passthrough).
*   **Intelligent Assistance:** Integrates with a **Large Language Model (LLM)** to provide users with detailed contextual explanations and treatment solutions based on the detected pathology.
*   **Immersive Passthrough Experience:** Developed using the **Meta XR SDK** and **OpenXR**, offering a fluid mixed reality interaction that allows the user to remain connected to the real environment while analyzing crops.

## 🛠️ Technologies Used

*   **Game Engine & AR Development:** Unity (C#)
*   **XR Frameworks:** Meta XR SDK, OpenXR, XR Interaction Toolkit
*   **Machine Learning / Computer Vision:** 
    *   YOLO26s (disease detection and classification)
    *   ONNX Runtime (neural inference integrated within Unity)
    *   LLM API Integration
*   **Programming Languages:** C# (XR application logic), Python (visual recognition model training)

## 🚀 Installation and Usage

### System Requirements
*   **Hardware:** Meta Quest Headset (Quest 3 / Quest Pro recommended for optimal color passthrough).
*   **Software:** Unity 2022.3 LTS (or newer) with the Android Build Support module installed.

### Steps to run the project
1. Clone this repository:
   ```bash
   git clone https://github.com/IonutMocanu/HackatonXR.git
   ```
2. Open the cloned project using **Unity Hub**.
3. Check in the *Package Manager* that the Meta XR packages and the ONNX plugin are imported correctly.
4. Connect your Meta Quest headset to the PC with Developer Mode enabled.
5. In Unity, navigate to `File -> Build Settings`, ensure the selected platform is **Android**, and click **Build and Run**.

## 👨‍💻 Author

*   **Mocanu Andrei Ionuț**

## 📄 License

This project is licensed under the MIT License.
