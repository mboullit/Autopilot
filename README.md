# Autopilot

This is the depository of our Autopilot project, Welcome!

The goal is to make an AI to drive a car on two circuits. The simulation of the car is done entirely under Unity, and the AI to drive the car has a steering wheel, brakes, gas pedals, and one or more cameras. The cameras provide images that can be analyzed. welcome !  

## Team members  

- Boullit Mohamed Fayçal

- Zhou Patrice  

## Project description  

*This deposit contains two main folders:*

- Assets : contains the prefab and model used in this project.  

- Results : contains the results of the different trained models:
		- The folder Pilote_Cam_AZ: contains the results and trained models for the bird's eye view camera approach.
		- The folder Pilote_Front_Cam: contains the results and trained models for the front camera approach.
  
## Requirement

  Unity is required for this project, with the import of [Unity Machine Learning Agents Toolkit](https://github.com/Unity-Technologies/ml-agents) used for trainning models.

## Setting the environment
In order to launch the project with a trained model, it's essential to set the corresponding camera with the desired model in the Behavior Parameters and Camera Sensor components:
- ![Setting environment](/Figs/RLSettings.PNG)

## Analyzes
To monitor the statistics of Agent performance during training, use [TensorBoard](https://github.com/Unity-Technologies/ml-agents/blob/master/docs/Using-Tensorboard.md). In a new terminal:
-   Go to the project directory,
-   Type  `tensorboard --logdir results --port 6006`
-   Open a browser window and navigate to  `localhost:6006`
