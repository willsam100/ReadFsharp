module AppHandlers

open System
open Microsoft.Extensions.Logging
open Giraffe
open Microsoft.AspNetCore.Http
open System.Threading.Tasks

type Resp = {
    Response: string[]
}

[<CLIMutable>]
type ConvertRequest = {
    CodeBlocks: string[]
}

let cors  : HttpHandler =
    setHttpHeader "Access-Control-Allow-Origin" "*"
    >=> setHttpHeader "Access-Control-Allow-Credentials" "true"

let parsingError (err : string) = RequestErrors.BAD_REQUEST err

let processCode (codeBlocks:string[]) = 

    printfn "%s" codeBlocks.[0]

    let results = 
        codeBlocks
        |> Array.map (fun csharp -> 
                try
                    FSharper.Core.Converter.runWithConfig false csharp
                with e -> e.Message )
    { Response = results}
            
let indexHandler  =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        text "Serverless Giraffe Web API" next ctx

let notFound  (next : HttpFunc) (ctx : HttpContext) =
    let msg = sprintf "Not found: (%s) (%s) (%s)" (ctx.GetRequestUrl()) (ctx.Request.Path.Value) (ctx.Request.PathBase.Value)
    (setStatusCode 404 >=> text msg) next ctx


let webApp: HttpHandler =  
    subRoute "/proxy+"
       (choose [
            GET >=>
                choose [
                    routex "/ping" >=> cors >=> json ({Response = [|"pong" |] })
                    routex "/api" >=> cors >=> indexHandler
                    routex "/" >=> cors >=> indexHandler 
                ]
            POST >=> 
                choose [
                    route "/csharp" >=> cors >=> bindJson<ConvertRequest> (fun car -> car.CodeBlocks |> processCode |> json) 
                ]
            notFound
            ])
        
//------------------------------
// Error handler
// ---------------------------------
let errorHandler (ex : Exception) (logger : ILogger) =
    logger.LogError(EventId(), ex, "An unhandled exception has occurred while executing the request.")
    clearResponse >=> setStatusCode 500 >=> text ex.Message
    
    