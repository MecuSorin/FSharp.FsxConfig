module Program

open Expecto

type MyConfiguration = {
    athing: int
    something: string
}
module MyConfiguration =
    let internal Default: MyConfiguration = { athing = 1; something = "default value" }

let workingTest pathPrefix = test "3 scripts are applied" {
    let applyScriptConfiguration path config =
        let scriptFile = System.IO.Path.Combine(pathPrefix, path)
        printfn "Using script: %s" scriptFile
        FSharp.FsxConfig.applyScriptConfiguration scriptFile config
    let actual =
        MyConfiguration.Default
        |> applyScriptConfiguration "changing athing copy.fsx"
        |> Result.bind (applyScriptConfiguration "changing_something.fsx")
        |> Result.bind (applyScriptConfiguration "changingathing.fsx")

    let expected = { athing = 21; something = "customized" } |> Ok

    Expect.equal actual expected "Should be the same"
}

let noFunctionTest pathPrefix = test "A script without the changeConfiguration function" {
    let applyScriptConfiguration path config =
        let scriptFile = System.IO.Path.Combine(pathPrefix, path)
        printfn "Using script: %s" scriptFile
        FSharp.FsxConfig.applyScriptConfiguration scriptFile config
    let actual =
        MyConfiguration.Default
        |> applyScriptConfiguration "changing athing copy.fsx"
        |> Result.bind (applyScriptConfiguration "changing_something.fsx")
        |> Result.bind (applyScriptConfiguration "invalidScript missing fn.fsx")
        |> Result.bind (applyScriptConfiguration "changingathing.fsx")

    Expect.isError actual "Should fail on invalid script"
}

let wrongFunctionInputTypeTest pathPrefix = test "A script with wrong input type for function" {
    let applyScriptConfiguration path config =
        let scriptFile = System.IO.Path.Combine(pathPrefix, path)
        printfn "Using script: %s" scriptFile
        FSharp.FsxConfig.applyScriptConfiguration scriptFile config
    let actual =
        MyConfiguration.Default
        |> applyScriptConfiguration "changing athing copy.fsx"
        |> Result.bind (applyScriptConfiguration "changing_something.fsx")
        |> Result.bind (applyScriptConfiguration "invalidScript wrong input type.fsx")
        |> Result.bind (applyScriptConfiguration "changingathing.fsx")

    Expect.isError actual "Should fail on invalid script"
}

let wrongFunctionOutputTypeTest pathPrefix = test "A script with wrong output type of function" {
    let applyScriptConfiguration path config =
        let scriptFile = System.IO.Path.Combine(pathPrefix, path)
        printfn "Using script: %s" scriptFile
        FSharp.FsxConfig.applyScriptConfiguration scriptFile config
    let actual =
        MyConfiguration.Default
        |> applyScriptConfiguration "changing athing copy.fsx"
        |> Result.bind (applyScriptConfiguration "changing_something.fsx")
        |> Result.bind (applyScriptConfiguration "invalidScript wrong output type.fsx")
        |> Result.bind (applyScriptConfiguration "changingathing.fsx")

    Expect.isError actual "Should fail on invalid script"
}

let allTests pathPrefix =
    [
        wrongFunctionInputTypeTest
        wrongFunctionOutputTypeTest
        noFunctionTest
        workingTest
    ]
    |> List.map (fun t -> t pathPrefix)
    |> testList "Tests"

[<EntryPoint>]
let main args =
    FSharp.FsxConfig.verbose <- true
    let pathPrefix =
        args
        |> Array.tryFind (fun x -> x.ToLower().StartsWith("scriptpath>"))
        |> function
            | Some scriptpath -> scriptpath.Substring("scriptpath>".Length)
            | None -> "."

    runTestsWithCLIArgs [] [||] (allTests pathPrefix)