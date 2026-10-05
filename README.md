# Neurotechnologies Lab Project Template

Template for the Neurotechnologies lab. It aims to give the student the resources necessary for developing a BCI application in Unity.

# Table of Contents
  * [Prerequisites](#prerequisites)
  * [Installation](#installation)
  * [Usage](#usage)

## Prerequisites

- A CLI or GUI with support for Git. For example, i use [git-bash](https://gitforwindows.org/) as a CLI, and you can use [Github Desktop](https://desktop.github.com/) for GUI
- [Unity Editor](https://unity.com/download) version 2022.3.62f3 or later
- [Unicorn Suite Hybrid Black](https://drive.google.com/drive/folders/17qbtoRuF21MZq9gWymBsGcsaGAOl3G7E?usp=sharing)
- [Unicorn Unity API Package](https://drive.google.com/file/d/1BtasXZZyl4hO5_aelALFlScgwdgIKI-u/view?usp=sharing) (Optional, for those that have already started a project and want to use it instead of this template)

## Installation

To get this Unity project template up and running, you need to follow these steps:

1. Clone the repository in a folder of your liking ([This](https://youtu.be/bQrtezWlphU?si=N-UqagFMgqONtPYw&t=42) is how to clone a repository wtih CLI, and [This](https://youtu.be/PoZNIbs_wx8?si=zmT12G9UFDycPjFg) is for GUI)
2. Open Unity Hub and select the Open button to select a project (The button may not say exactly "Open", depending on your Unity Hub version, but it should be similar)

![Unity Hub](https://github.com/Relu12345/BCI-Lab-Project/assets/94746838/ad9dc154-1d5d-42d8-98b9-2eb3d2f00f63)


3. Select the project folder that you just cloned
 
![Unity Hub Select Folder](https://github.com/Relu12345/BCI-Lab-Project/assets/94746838/db8bc609-6c7c-4930-bf76-2698294826c8)

4. Now your project should be openning, give it a minute or two.

- ### Even if you do not have exactly version 2022.3.62f3 of Unity, just make sure you have a version newer than 2022.3.62f3 and you can proceed with opening the project while ignoring the warning

![Warning Unity Hub Project](https://github.com/Relu12345/BCI-Lab-Project/assets/94746838/86505df6-cdc7-406a-a5bd-d760e66146d5)

You are good to go! We will now take a look at what is available for us to use when developing our BCI Game.

## Usage

0. Before running the project:
   - Open Unicorn Suite
   - Connect your Unicorn Hybrid Black device
   - Make sure the device is detected and streaming properly

1. After your project has opened, navigate inside the `Assets/g.tec/Unity Interface/Prefabs/BCI` folder.

![Unity Interface](https://github.com/user-attachments/assets/ac058890-c2d2-43b1-8522-f1babbb6ee53)

2. Here, you are presented with the main components of the Unicorn ERP Unity Interface:

![Unity Prefabs Folder](https://github.com/user-attachments/assets/f185d3c8-a46d-4b39-816b-7055ce99ad92)

- **BCI Visual ERP 2D/3D**: These are the main components of the ERP Interface, you can use whichever fits your game idea, 2D or 3D.
  
3. Drag and drop a BCI Visual ERP in your hierarchy / in your scene, coresponding to your game's perspective (2D or 3D). I will use 3D.

![BCI Visual ERP](https://github.com/user-attachments/assets/3951935f-99d0-4554-a2bc-8c2935505ff0)

### Elements of the BCI Visual ERP component
- **UI/BCIBarDocker_UI**: The main UI element of the ERP manager. Here, in the BCIUI script in the inspector, you can make your own UI elements, or use the ones available by default.
- **Device/Pipelines**: Here you have the pipelines used by the ERP manager. You can just leave the as default for a classic BCI application, or you can add/create new ones. For those that want to also work with the EEG data, they can remove the `ERPPipeline` and add the `EEGData` pipeline from `Assets/g.tec/Unity Interface/Prefabs/Pipelines`, which gives you either Raw data, or pre-processed data using g.tec's in-house algorithm.

### Unicorn Hybrid Black Unity Pipelines

- **ERPPipeline**: The main pipeline used to handle ERP logic in your application. The default BCI Visual ERP 2D/3D's version also comes with the ERPParadigm pipeline, which is used for the settings of the objects used for flashing in this application. You can also used the advanced settings section to modify things like the threshold at which the selection is made and logging the data to CSV, but anything beyond that should be done only after researching thoroughly about each topic. 
- **BatteryLevelPipeline**: A pipeline with an UI element that shows the current battery level of the headset.
- **SignalQualityPipeline**: A pipeline with an UI element that continuously estimates the signal quality of the EEG provided by the attached device. You can modify the advanced settings if needed, but for most surface EEG cases, it is recommended to use the default settings.
- **DataLostPipeline**: Another pipeline with an UI element that shows up whenever there are packets lost from the headset. You can also set what should happen when there is data lost.

### The ERP Pipeline

Here you will make the most changes, as they are needed for getting your application to an usable state.

- **Flash Mode**: `ERP` or `ERP No Overlap`. The second option is used if you want to have the flashing be done without Off time.
- **Number Of Training Trials**: This denotes how many times an object should flash before the training part is considered finished. The more training trails, the longer the training process, but also the better results, and vice-versa. 
- **Number Of Classes**: This sets how many different types of ERP objects you want to use in the application. Keep in mind that the bigger this number is, the more it takes for the application to go through all of the objects, so selection of an object will take a lot longer than using a small number. The limits for the number of classes is 2-60, with anything below or above giving an error.
- **On Time Ms**: This represents how much time should the flashing part be shown when selectin an object, in milliseconds.
- **Off Time Ms**: Same as above, but for how long an object should stay in it's default form before selecting. This is not present when using `ERP No Overlap` flash mode.

![ERP Pipeline](https://github.com/user-attachments/assets/520f752b-a1ed-417b-9cb6-2036d700b188)

### ERP Flash Tags

In the ERP Visual ERP object, inside the ERPPipeline in it's hierarchy (`Device/Pipelines/ERPPipeline/ERPParadigm/ERPTags`), you can find the flash objects used in the application itself. The default prefab comes with 5 of them, one of which is the training object needed at the start of the runtime.

- **Is Training Object**: A bool that represents if the object will be used for training by the user, to make the end-user be able to select the other objects in the game. The training object has to be the same as (or atleast extremely similar to the) objects in the game, if you want the game to be any playable. Note: You can use the same `Class Id` for application objects too.
- **Class Id**: This is the classification id of the object, used so that you can programmatically select objects.
- **Flash and Dark Material / Sprite**: These are used to represent the training and BCI object's visual states, with the dark material / sprite (depending if you are making a 3D, respectively 2D game) representing the base material / texture of your object, and the flash material / sprite representing the object's visual appearance when flashing.
- **Material Index**: This is used for changing the material of the current experiment.

![Training Object](https://github.com/user-attachments/assets/a9aed41b-4d57-41b8-b931-282c4b711e90)

If you have the number of classes less or more than the limit, you will get an error similar to this, keep in mind.

![Objects Error](https://github.com/Relu12345/BCI-Lab-Project/assets/94746838/8e588205-e088-43f8-aea4-6c17f0a96dde)

### Making your own code

Here comes the fun part. There are a lot of event callbacks in the Unity package used for BCI development, which offer you tons of functionality that can be customized. You will most probably use, almost in exclusivity, the ERPPipeline's `On Class Selection` callback event. Here is an example of creating a new script, and then using it to show which class was selected. The code for our script, `BCISelection`:

```
using Gtec.Chain.Common.Templates.Utilities;
using Gtec.UnityInterface;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BCISelection : MonoBehaviour
{
    public void OnClassificationSelection(ERPPipeline erpPipeline, ClassSelection classSelection)
    {
        Debug.Log("ClassSelection: " + classSelection.Class.ToString());
    }
}
```

Then, in the ERPPipeline, you add the script and it's function to the callback event.

![Callblack event](https://github.com/user-attachments/assets/1508a6e1-29f8-4bd5-866c-7967df64a2d1)

This will make it so when you are selecting a certain object, you will see a log message saying which object was used. From here on, it is all about finding uses for this by yourselves, be creative.

You are done with the setup instructions! You can go wild now =)
#   B C I - C h e s s  
 