namespace SecureWebShop

open System
open System.Text
open System.Net
open Suave.Web
open Suave.Http
open Suave.WebPart
open Suave.Successful
open Suave.Filters
open Suave.Operators
open BCrypt.Net

module Program =
    let private _accounts = [
        ("admin", BCrypt.HashPassword("admin"))
        ("guest", BCrypt.HashPassword("guest"))
    ]

    let private authenticate(username: String, password: String) =
        _accounts        
        |> List.exists(fun (user, hash) ->
            let usernameMatch = user.Equals(username, StringComparison.Ordinal)
            let passwordMatch = BCrypt.Verify(password, hash)
            usernameMatch && passwordMatch
        )

    let private webServerCfg(host: String, port: Int32) = { 
            defaultConfig with
                bindings = [HttpBinding.create HTTP (IPAddress.Parse host) (uint16 port)]
                listenTimeout = TimeSpan.FromMilliseconds (2000.)
        }

    let private tryGetParameter(ctx: HttpContext, name: String) =
        match ctx.request.formData name with
        | Choice1Of2 parameterValue -> Some parameterValue
        | _ -> None

    let private login(ctx: HttpContext) =
        OK """
        <html>
        <head><title>Welcome to the login of the Secure Web Shop</title></head>
        <body>
            <h1>-= Secure Web Shop Login =-</h1>
            <form method="POST" action="/login">
                <table>
                    <tr>
                        <td>Username:</td>
                        <td><input type="text" name="username"></td>
                    </tr>
                    <tr>
                        <td>Password:</td>
                        <td><input type="password" name="password"></td>
                    </tr>
                    <tr>
                        <td></td>
                        <td><input type="submit" name="Login"></td>
                    </tr>
                </table>
            </form>
        </body>
        </html> 
        """ ctx

    let private doLogin(ctx: HttpContext) =
        match (tryGetParameter(ctx, "username"), tryGetParameter(ctx, "password")) with
        | (Some username, Some password) when authenticate(username, password) -> OK "Authentication successfully executed!" ctx
        | _ -> OK "You entered the wrong username/password combination" ctx

    let private webServerRoutes = 
        choose [
            GET >=> choose [
                path "/" >=> login
                path "/login" >=> login
            ]

            POST >=> choose [             
                path "/login" >=> doLogin
            ]
        ]

    let private startWebServer(host: String, port: Int32) =
        let cfg = webServerCfg(host, port)
        startWebServer cfg webServerRoutes

    [<EntryPoint>]
    let main argv = 
        printfn "-= Secure Web Shop =-"
        printfn "Start web server on 127.0.0.1:8080"
        startWebServer("127.0.0.1", 8080)
        0
