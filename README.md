# 👁️ Look & RST: Gaze-Based Object Manipulation in VR

**Author:** Pratik Mohanty\
**Advisors:** Ken Pfeuffer & Uta Wagner\
**Institution:** Aarhus University, Department of Computer Science\
**Thesis Date:** June 2025



---

## 🧠 Overview

**Look & RST** explores multimodal interaction in immersive Virtual Reality (VR) by enabling intuitive object **Rotation, Scaling, and Translation (RST)** using a combination of **gaze** and **hand gestures**. The project evaluates three novel techniques:

- **Hand Ray** (conventional ray-based manipulation)
- **Gaze + Pinch** (eye-based selection + hand gesture control)
- **Look&Drop** (primarily gaze-based with minimal hand input)

This Unity-based VR application supports manipulation tasks in both **2D and 3D**, and is designed to be ergonomic and intuitive—ideal for extended spatial design work.

---

## 🎯 Goals

- Using 3 Different interactions technique for Object Manipulation
- Compare three multimodal techniques through a **user study** with 18 participants.
- Offer a **PowerPoint-like** interface for spatial design in VR.

---

## 🕹️ Interaction Techniques

| Technique        | Description                                                        |
| ---------------- | ------------------------------------------------------------------ |
| **Hand Ray**     | Standard ray-based manipulation using hand gestures.               |
| **Gaze + Pinch** | Look to select, pinch to manipulate in XYZ.                        |
| **Look&Drop**    | Use gaze for XY control, pinch for Z movement. Minimizes hand use. |

---

## 💠 Features

- 🌐 **2D/3D Mode Switching**
- ✋ **Multi-object Selection**
- 🎨 **Color Picker & Freehand Drawing**
- 🎤 **Voice Annotation Support**
- 🧠 **Visual Feedback for Gaze & Gesture States**

---

## 📊 Evaluation Highlights

- **Gaze + Pinch** was preferred by \~70% of users across all RST tasks.
- **Look&Drop** reduced physical strain but required higher mental effort.
- **Hand Ray** was intuitive but physically fatiguing during prolonged tasks.

---

## 🧪 Technologies Used

- **Unity (C#)**
- **Meta Quest Pro** (eye and hand tracking)
- **Meta XR SDK**
- **One Euro Filter** for smoothing gaze/gesture noise
- **Physics Raycasting & SphereCasting** for interaction detection

---

## 🧱 Directory Structure

```
/Assets
  /Scripts
    - Handrayinteraction.cs
    - iiihandrayinteraction.cs
  /Prefabs
  /UI
/Scenes
/Docs
  - Thesis.pdf
README.md
```

---

## 🧹 How to Run

1. Clone the repository:

   ```bash
   git clone https://github.com/pratikmohanty1425/LookNRST.git
   cd LookNRST
   ```

2. Open in **Unity 2023+** with Meta XR SDK installed.

3. Load the `MainScene.unity` and deploy to Meta Quest Pro.

---

## 📌 Future Work

- Expand to collaborative multi-user VR environments.
- Integrate precision modes for professional 3D modeling.
- Explore adaptive UI elements based on gaze heatmaps.

---

## 📄 License

This project is licensed under the MIT License.

---

## 🙏 Acknowledgments

Thanks to Ken Pfeuffer, Uta Wagner, and the EH research group at Aarhus University for their support and guidance.

