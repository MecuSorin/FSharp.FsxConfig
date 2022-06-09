module FSharp.FsxConfig
    open Thoth.Json.Net

    let mutable verbose = false

    let serialize ac = Encode.Auto.toString(0, ac)

    module internal FSI =
        open System.IO
        open System.Text
        open FSharp.Compiler.Interactive.Shell

        let evalScript scriptPath (fsiSession: FsiEvaluationSession) =
            match fsiSession.EvalScriptNonThrowing scriptPath with
            | Choice1Of2 (), _ -> Ok fsiSession
            | Choice2Of2 exn, aa ->
                if verbose then printfn "Eval script error %A" aa
                Error exn.Message
        let evalExpression (scriptText: string) (fsiSession: FsiEvaluationSession) =
            match fsiSession.EvalExpressionNonThrowing scriptText with
            | Choice1Of2 (Some value), _ -> value.ReflectionValue |> unbox<string> |> Ok
            | Choice1Of2 None, _ -> Error "No value resulted from expression"
            | Choice2Of2 exn, aa ->
                if verbose then printfn "Eval change function error %A" aa
                Error exn.Message
        let evalReference (fsiSession: FsiEvaluationSession) =
            let reference = $"#r @\"{ System.Reflection.Assembly.GetCallingAssembly().Location }\""
            if verbose then printfn "reference: %s" reference
            match fsiSession.EvalInteractionNonThrowing reference with
            | Choice1Of2 (Some _value), _ -> Ok fsiSession
            | Choice1Of2 None, _ -> Ok fsiSession
            | Choice2Of2 exn, aa ->
                if verbose then printfn "Eval reference error %A" aa
                Error exn.Message
        let getNamespaceForScript (scriptPath: string) =
            let name = System.IO.Path.GetFileNameWithoutExtension scriptPath    // not checking for invalid input
            if 1 < name.Length
                then "``" + System.Char.ToUpper(name[0]).ToString() + name.Substring(1) + "``"
                else name.ToUpper()

        let executeScript scriptPath (currentConfig: 'c) =
            if System.String.IsNullOrWhiteSpace scriptPath
                then Error "Invalid script path"
                else
                    let deserialize =
                        let decoder = Decode.Auto.generateDecoderCached<'c>()
                        Decode.fromString decoder

                    // Initialize output and input streams
                    let sbOut = StringBuilder()
                    let sbErr = StringBuilder()
                    use inStream = new StringReader("")
                    use outStream = new StringWriter(sbOut)
                    use errStream = new StringWriter(sbErr)
                    let fsiArgs = [| "dotnet"; "fsi"; "--noninteractive" |]
                    let fsiConfig = FsiEvaluationSession.GetDefaultConfiguration()
                    use fsiSession = FsiEvaluationSession.Create(fsiConfig, fsiArgs, inStream, outStream, errStream, collectible = true)
                    let currentConfigParameterLabel = "currentConfig"
                    let expressionToEvaluate = $"{ getNamespaceForScript scriptPath }.changeConfiguration { currentConfigParameterLabel } |> FSharp.FsxConfig.serialize"
                    // printfn "Executing in FSI: %s" expressionToEvaluate
                    fsiSession.AddBoundValue(currentConfigParameterLabel, currentConfig)
                    fsiSession
                    |> evalReference
                    |> Result.bind (evalScript scriptPath)
                    // |> Result.bind (evalExpression "open FSharp.FsxConfig")
                    |> Result.bind (evalExpression  expressionToEvaluate)
                    |> Result.bind deserialize

    let applyScriptConfiguration s c =
        if verbose then printfn "Targeting script: %s" s
        FSI.executeScript s c