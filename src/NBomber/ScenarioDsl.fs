namespace rec NBomber

open System
open System.Collections.Generic
open System.Threading
open System.Threading.Tasks
open System.Runtime.InteropServices

open FSharp.Control.Tasks.V2.ContextInsensitive

type StepName = string
type Request = obj

[<Struct>]
type Response = {
    IsOk: bool
    Payload: obj
} with
  static member Ok([<Optional;DefaultParameterValue(null:obj)>]payload: obj) = { IsOk = true; Payload = payload }
  static member Fail(error: string) = { IsOk = false; Payload = error }

type Step = {
    StepName: StepName
    Execute: Request -> Task<Response>
} with
  static member Create(name: StepName, execute: Func<Request,Task<Response>>) =
    { StepName = name; Execute = execute.Invoke }

  static member CreatePause(delay: TimeSpan) =    
    { StepName = "pause"
      Execute = (fun req -> task { do! Task.Delay(delay) 
                                   return Response.Ok(req) }) }

type TestFlow = {
    FlowName: string
    Steps: Step[]
    ConcurrentCopies: int
}

type Scenario = {
    ScenarioName: string
    InitStep: Step option
    Flows: TestFlow[]
    Duration: TimeSpan
}

type ScenarioBuilder(scenarioName: string) =
    
    let flows = Dictionary<string, TestFlow>()
    let mutable initStep = None    

    let validateFlow (flow) =
        let uniqCount = flow.Steps |> Array.map(fun c -> c.StepName) |> Array.distinct |> Array.length
        
        if flow.Steps.Length <> uniqCount then
            failwith "all steps in test flow should have unique names"

    member x.Init(initFunc: Func<Request,Task<Response>>) =
        let step = { StepName = "init"; Execute = initFunc.Invoke }
        initStep <- Some(step)
        x    

    member x.AddTestFlow(flow: TestFlow) =
        validateFlow(flow)        
        flows.[flow.FlowName] <- flow
        x

    member x.AddTestFlow(name: string, steps: Step[], concurrentCopies: int) =
        let flow = { FlowName = name; Steps = steps; ConcurrentCopies = concurrentCopies }
        x.AddTestFlow(flow)

    member x.Build(duration: TimeSpan) =
        let testFlows = flows
                        |> Seq.map (|KeyValue|)
                        |> Seq.map (fun (name,job) -> job)
                        |> Seq.toArray

        { ScenarioName = scenarioName
          InitStep = initStep
          Flows = testFlows
          Duration = duration }


module FSharpAPI =

    let scenario (scenarioName: string) =
        { ScenarioName = scenarioName
          InitStep = None
          Flows = Array.empty
          Duration = TimeSpan.FromSeconds(10.0) }

    let init (initFunc: Request -> Task<Response>) (scenario: Scenario) =
        let step = { StepName = "init"; Execute = initFunc }
        { scenario with InitStep = Some(step) }

    let addTestFlow (flow: TestFlow) (scenario: Scenario) =
        { scenario with Flows = Array.append scenario.Flows [|flow|] }

    let build (interval: TimeSpan) (scenario: Scenario) =
        { scenario with Duration = interval }