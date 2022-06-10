module FSharp.FsxConfig

    let mutable verbose = false

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
        let evalExpression<'t> (scriptText: string) (fsiSession: FsiEvaluationSession) =
            match fsiSession.EvalExpressionNonThrowing scriptText with
            | Choice1Of2 (Some value), _ -> value.ReflectionValue |> unbox<'t> |> Ok
            | Choice1Of2 None, _ -> Error "No value resulted from expression"
            | Choice2Of2 exn, aa ->
                if verbose then printfn "Eval change function error %A" aa
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
                    let changeConfigurationScriptFunctionName = $"{ getNamespaceForScript scriptPath }.changeConfiguration"
                    let expressionToEvaluate = $"{ changeConfigurationScriptFunctionName } { currentConfigParameterLabel } "//|> FSharp.FsxConfig.serialize"
                    // printfn "Executing in FSI: %s" expressionToEvaluate
                    fsiSession.AddBoundValue(currentConfigParameterLabel, currentConfig)
                    fsiSession
                    |> evalScript scriptPath
                    |> Result.bind (evalExpression  expressionToEvaluate)

    let applyScriptConfiguration s c =
        if verbose then printfn "Targeting script: %s" s
        FSI.executeScript s c