namespace LiteDB.FSharp

open System.Reflection
open LiteDB
open System
open System.Collections.Generic
open System.Linq.Expressions
open Microsoft.FSharp.Reflection
open Newtonsoft.Json
open LiteDB

type FSharpBsonMapper() = 
    inherit BsonMapper()
    let entityMappers = Dictionary<Type,EntityMapper>() 
    member this.DbRef<'T1,'T2> (exp: Expression<Func<'T1,'T2>>) =
        this.Entity<'T1>().DbRef(exp) |> ignore
    
    static member RegisterInheritedConverterType<'T1,'T2>() =
        let t1 = typeof<'T1>
        let t2 = typeof<'T2>
        Cache.inheritedConverterTypes.AddOrUpdate(
            t1.FullName,
            HashSet [t2],
            ( fun _ types -> types.Add(t2) |> ignore; types )
        ) |> ignore




    override self.ToObject(entityType: System.Type, entity: BsonDocument) = Bson.deserializeByType entity entityType 
    override self.ToObject<'t>(entity: BsonDocument) = Bson.deserialize<'t> entity
    override self.ToDocument<'t>(entity: 't) = 
        //Add DBRef Feature :set field value with $ref  
        if typeof<'t>.FullName = typeof<BsonDocument>.FullName 
        then entity |> unbox<BsonDocument>
        else
        let withEntityMap (doc:BsonDocument)=
            let mapper = entityMappers.Item (entity.GetType())
            for memberMapper in mapper.Members do
                if not (isNull memberMapper.Serialize) then  
                    let value = memberMapper.Getter.Invoke(entity)
                    let serialized = memberMapper.Serialize.Invoke(value, self)
                    doc.RawValue.[memberMapper.FieldName] <- serialized
            doc
        Bson.serialize<'t> entity
        |> withEntityMap 
        
    override self.BuildAddEntityMapper(entityType)=
        let mapper = base.BuildAddEntityMapper(entityType)
        entityMappers.Add(entityType, mapper)
        mapper

    override self.GetTypeCtor(mapper) =
        if FSharpType.IsRecord(mapper.ForType, BindingFlags.Public ||| BindingFlags.NonPublic) then
            CreateObject (fun doc -> FSharpBsonMapper().ToObject(mapper.ForType, doc))
        else
            base.GetTypeCtor(mapper)
    
    override self.ToDocument(``type``, entity) =
        //Add DBRef Feature :set field value with $ref  
        if ``type``.FullName = typeof<BsonDocument>.FullName 
        then entity |> unbox<BsonDocument>
        else
        let withEntityMap (doc:BsonDocument)=
            let mapper = entityMappers.Item (entity.GetType())
            for memberMapper in mapper.Members do
                if not (isNull memberMapper.Serialize) then  
                    let value = memberMapper.Getter.Invoke(entity)
                    let serialized = memberMapper.Serialize.Invoke(value, self)
                    doc.RawValue.[memberMapper.FieldName] <- serialized
            doc
        Bson.serializeBase entity ``type``
        |> withEntityMap 
        
        
    

    
