namespace ReadFsharp.Tests


open Xunit
open Amazon.Lambda.TestUtilities
open Amazon.Lambda.APIGatewayEvents

open System.IO
open Newtonsoft.Json

open Setup


module HttpHandlersTests =

    [<Fact>]
    let ``Request HTTP Get at /ping``() = async {
        let request = APIGatewayProxyRequest(Path = "/proxy+/ping", HttpMethod = "GET")
        let! response = LambdaEntryPoint().FunctionHandlerAsync(request, TestLambdaContext()) |> Async.AwaitTask
        Assert.Equal(200, response.StatusCode)
    }

    [<Fact>]
    let ``Request HTTP Get at /csharp``() = async {

        let csharp = 
            """{"CodeBlocks":["public class Foo { public int Foo = 42;}"]}"""        
        let expectedFsharp = """{"response":["type Foo() =\n    member this.Foo = 42"]}"""

        let request = APIGatewayProxyRequest(Path = "/proxy+/csharp", HttpMethod = "POST", Body = csharp)
        let! response = LambdaEntryPoint().FunctionHandlerAsync(request, TestLambdaContext()) |> Async.AwaitTask
        Assert.Equal(expectedFsharp, response.Body)
    }

    [<EntryPoint>]
    let main _ = 0
