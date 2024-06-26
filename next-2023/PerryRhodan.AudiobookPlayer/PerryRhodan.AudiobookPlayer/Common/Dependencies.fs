module Dependencies

open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting


let sp = ServiceCollection()


type DependencyService() =
    
    static member Get<'a>()=
        sp.BuildServiceProvider().GetService<'a>()
        
    static member ServiceCollection with get() = sp

