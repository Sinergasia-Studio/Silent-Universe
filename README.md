# Project Description

A modular Unity game system prototype focused on scalable architecture, reusable gameplay mechanics, and clean separation of concerns.
This project demonstrates how multiple gameplay systems (AI, environment interaction, checkpoints, CCTV mechanics, repair mechanics, and event-driven logic) can be organized into independent modules suitable for solo developers aiming for production-ready code quality.

Designed with maintainability and extensibility in mind, each feature is implemented as a loosely coupled system using assembly definitions and cross-scene event channels to reduce dependencies between modules.

## Key Features
Modular Architecture <br>
Systems are separated into independent modules (Environment, GameSystems, CCTV, Etc) <br>
Clear boundaries between gameplay logic and shared core functionality <br>
Scalable structure suitable for mid-to-large solo projects <br>

## Gameplay Systems <br>

CCTV camera system with save & restore functionality <br>
Enemy AI with peek points and environmental awareness <br>
Checkpoint system for progress tracking <br>
Fuse box and electrical interaction mechanics <br>
Disk repair interaction system <br>
Choppable objects for environmental interaction <br>
Footstep system for player feedback and immersion <br> 
Dampener state controller for dynamic gameplay effects <br>

## Event-driven communication <br>

Cross-scene event channels used to reduce tight coupling <br>
Flexible communication between systems without direct references <br>

Production-oriented structure <br>

Organized using Assembly Definition Files (.asmdef) <br>
Clean folder hierarchy based on responsibility <br>
Designed for easy feature expansion and refactoring <br>
Goal of the Project <br>

This repository serves as a foundation for building a complete game while maintaining code clarity and flexibility. <br>
It is particularly suited for solo developers who want to:

learn scalable Unity architecture <br>
build reusable gameplay systems <br>
avoid tightly coupled code <br>
maintain long-term project stability <br>
Tech Stack <br>
Unity (C#) <br>
Assembly Definition modularization <br>
Event-driven architecture pattern <br>
Status : <br>

Work in progress — systems are continuously being improved and refactored toward production-ready standards.
