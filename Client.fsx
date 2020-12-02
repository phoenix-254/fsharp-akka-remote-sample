#r "nuget: Akka.FSharp"
#r "nuget: Akka.Remote"

#load @"./Messages.fsx"

open Akka.Actor
open Akka.FSharp
open Akka.Remote
open Akka.Configuration

open System


open Messages

type Init = { TotalUsers: int; }
type Shutdown = { Message: string; }

type Test = { TestInt: int; }
type TestSuccess = { TestBool: bool; }

// Remote Configuration
let configuration = 
    ConfigurationFactory.ParseString(
        @"akka {
            actor.provider = ""Akka.Remote.RemoteActorRefProvider, Akka.Remote""
            remote.helios.tcp {
                port = 6666
                hostname = localhost
            }
        }")

let system = ActorSystem.Create("Twitter", configuration)

let server = system.ActorSelection("akka.tcp://Twitter@localhost:5555/user/Server")

// Global supervisor actor reference
let mutable supervisor: IActorRef = null

type Client() = 
    inherit Actor()
    override x.OnReceive (message: obj) =
        match message with
        | :? Test as test -> 
            let request: TestMsgRequest = {
                TestInt = test.TestInt;
                TestStr = "TestRequest";
                TestBool = true;
            }
            server.Tell request
        | :? TestMsgResponse as response -> 
            printfn "Response from server: %A" response
            let res: TestSuccess = { TestBool = response.TestBool; }
            supervisor.Tell res
        | _ -> ()

type Supervisor() = 
    inherit Actor()
    let mutable i = 0
    let mutable n = 0
    let mutable parent: IActorRef = null
    override x.OnReceive (message: obj) = 
        match message with
        | :? Init as init -> 
            n <- init.TotalUsers
            parent <- x.Sender

            [1 .. n]
            |> List.iter (fun id -> let client = system.ActorOf(Props(typedefof<Client>), ("Client" + (id |> string)))
                                    let request: Test = { TestInt = id; }
                                    client.Tell request)
            |> ignore
        | :? TestSuccess as response -> 
            if response.TestBool then 
                i <- i + 1
                if i = n then
                    let res: Shutdown = { Message = "Done!"; }
                    parent.Tell res
            else printfn "Failure!"
        | _ -> ()

let CreateUsers(numberOfUsers: int) = 
    supervisor <- system.ActorOf(Props(typedefof<Supervisor>), "Supervisor")

    let (task:Async<Shutdown>) = (supervisor <? { TotalUsers = numberOfUsers; })
    let response = Async.RunSynchronously (task)
    printfn "%A" response
    supervisor.Tell(PoisonPill.Instance)

let args = Environment.GetCommandLineArgs()
CreateUsers(args.[3] |> int)
