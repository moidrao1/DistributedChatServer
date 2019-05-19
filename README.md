# Distributed Chat Server
Distributed Chat Server in C# using TCP Protocol.

This is a source code of distributed chat server developed in C#.

# Environment Requirement

1. Download Dot Net Core.
2. Download Visual Studio 2017
3. Clone Repository.

# BenchMark

Distributed Chat Server is load tested with following details:

1. Server Deployed on Separate 3 VM's. ( 3 Servers )
2. Load Balancer Deployed on Separate VM.
3. 3 Simulators deployed on Separate VM. ( 3 Vms with capacity to connect 33K clients each )
4. Transport protocol used : TCP

100 K Login Clients  on 3 Servers.

 "Simulator successfully achieved a rate of avg. 20k messages / second with less then 1 sec delay." 

# Limitation

In my load test , the limitation was route bandwidth. 

100MB router will chock if the packet size will increase to 150Bytes or more.

# Components Descriptions

There are total 4 components present in master branch:

1- Server Code
2- Client Code
3- Load Balancer Code
4- Simulator Code
5- Built Executables



# Chat Server

Chat Server is the main routing engine. The main responsibility is to connect with different clients simultaneously and route messages to destination client with minimum delay.
The Server should run before starting any client, its better to use multiple Vm's if you are load testing it.

# Chat Client

Chat Client is the client program which will connect to Server via Load Balancer ( available server ). It will send messages to your given destination client ID. Each Chat Client have its unique Client ID which will be used to send or receive messages.

# Load Balancer

We Limit each server to accept only 33K clients at a time and load balancer is used to divide equal load on each server. it will divide all incoming connection request to each server on the basis of availability and already connection count on each server.

# Simulator

Simulator is used to load test our distributed Solution. it will register 100K clients with distributed servers and send 12K-20K random messages with random client ID as destination.

