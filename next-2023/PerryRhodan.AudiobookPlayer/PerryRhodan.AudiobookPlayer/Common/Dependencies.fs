module Dependencies

open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting


let builder = Host.CreateApplicationBuilder([||])


type DependencyService() =
    
    static member Get<'a>()=
        builder.Services.BuildServiceProvider().GetService<'a>()

