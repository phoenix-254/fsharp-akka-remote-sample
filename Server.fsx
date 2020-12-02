#r "nuget: Akka.FSharp"
#r "nuget: Akka.Remote"

#load @"./Messages.fsx"

open Akka.Actor
open Akka.FSharp
open Akka.Remote
open Akka.Configuration

open System

open Messages

// Remote Configuration
let configuration = 
    ConfigurationFactory.ParseString(
        @"akka {
            actor.provider = ""Akka.Remote.RemoteActorRefProvider, Akka.Remote""
            remote.helios.tcp {
                port = 5555
                hostname = localhost
            }
        }")

let system = ActorSystem.Create("Twitter", configuration)

type BootServer = {
    BootMessage: string;
}

type ShutdownServer = {
    ShutdownMessage: string;
}

type Server() =
    inherit Actor()
    override x.OnReceive (message: obj) =   
        match message with 
        | :? BootServer as bootInfo -> 
            printfn "%s" bootInfo.BootMessage
        | :? TestMsgRequest as request ->
            printfn "Received request from client: %A" request
            let response: TestMsgResponse = {
                TestInt = 200;
                TestStr = "OK";
                TestBool = true;
            }
            x.Sender.Tell response
        | _ -> ()

let server = system.ActorOf(Props(typedefof<Server>), "Server")

let (task:Async<ShutdownServer>) = (server <? { BootMessage = "Server is running!"; })

let response = Async.RunSynchronously (task)
printfn "%A" response

server.Tell(PoisonPill.Instance);
