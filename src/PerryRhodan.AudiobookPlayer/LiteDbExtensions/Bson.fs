﻿namespace LiteDB.FSharp
//
// /// Utilities to convert between BSON document and F# types
// /// https://github.com/Zaid-Ajaj/LiteDB.FSharp/blob/master/LiteDB.FSharp/Bson.fs
// [<RequireQualifiedAccess>]
// module Bson =
//
//     open System
//     
//     open FSharp.Reflection
//     open Newtonsoft.Json
//     open LiteDB
//
//     /// Returns the value of entry in the BsonDocument by it's key
//     let read key (doc: BsonDocument) =
//         doc[key]
//
//     /// Reads a property from a BsonDocument by it's key as a string
//     let readStr key (doc: BsonDocument) = 
//         doc[key].AsString
//
//     /// Reads a property from a BsonDocument by it's key and converts it to an integer
//     let readInt key (doc: BsonDocument) = 
//         doc[key].AsString |> int
//
//     /// Adds an entry to a `BsonDocument` given a key and a BsonValue
//     let withKeyValue key value (doc: BsonDocument) = 
//         doc.Add(key, value)
//         doc
//
//     /// Reads a field from a BsonDocument as DateTime
//     let readDate (key: string) (doc: BsonDocument) = 
//         doc[key].AsDateTime
//
//     /// Removes an entry (property) from a `BsonDocument` by the key of that property
//     let removeEntryByKey (key:string) (doc: BsonDocument) = 
//         doc.Remove(key) |> ignore
//         doc
//
//     let private fsharpJsonConverter = FSharpJsonConverter()
//     let mutable internal converters : JsonConverter[] = [| fsharpJsonConverter |]
//     
//     /// Converts a typed entity (normally an F# record) to a BsonDocument. 
//     /// Assuming there exists a field called `Id` or `id` of the record that will be mapped to `_id` in the BsonDocument, otherwise an exception is thrown.
//     let serialize<'t> (entity: 't) = 
//         let typeName = typeof<'t>.Name
//         let json = JsonConvert.SerializeObject(entity, converters)
//         let doc = JsonSerializer.Deserialize(json) |> unbox<BsonDocument>
//         doc.Keys
//         |> Seq.tryFind (fun key -> key = "Id" || key = "id")
//         |> function
//           | Some key -> 
//              doc
//              |> withKeyValue "_id" (read key doc) 
//              |> removeEntryByKey key
//           | None -> 
//               let error = $"Exected type %s{typeName} to have a unique identifier property of 'Id' or 'id' (exact name)"
//               failwith error
//
//     /// Converts a BsonDocument to a typed entity given the document the type of the CLR entity.
//     let deserializeByType (entity: BsonDocument) (entityType: Type) = 
//     
//             let getCollectionElementType (collectionType:Type)=
//                 let typeNames = ["FSharpList`1";"IEnumerable`1";"List`";"IList`1"]
//                 let typeName = collectionType.Name
//                 if List.contains typeName typeNames then
//                     collectionType.GetGenericArguments().[0]
//                 else if collectionType.IsArray then
//                     collectionType.GetElementType()
//                 else failwithf "Could not extract element type from collection of type %s"  collectionType.FullName           
//             
//             let getKeyFieldName (entityType: Type)= 
//               if FSharpType.IsRecord entityType 
//               then FSharpType.GetRecordFields entityType 
//                    |> Seq.tryFind (fun field -> field.Name = "Id" || field.Name = "id")
//                    |> function | Some field -> field.Name
//                                | None -> "Id"
//               else "Id"
//                  
//             let rewriteIdentityKeys (entity:BsonDocument)=    
//                 
//                 let rec rewriteKey (keys:string list) (entity:BsonDocument) (entityType: Type) key =
//                     match keys with 
//                     | []  -> ()
//                     | y :: ys -> 
//                         let continueToNext() = rewriteKey ys entity entityType key 
//                         match y, entity.RawValue[y] with 
//                         // during deserialization, turn key-prop _id back into original Id or id
//                         | "_id", id ->
//                             entity
//                             |> withKeyValue key id
//                             |> removeEntryByKey "_id"
//                             |> (ignore >> continueToNext)
//                         
//                         |_, (:? BsonDocument as bson) ->
//                             // if property is nested record that resulted from DbRef then
//                             // also re-write the transformed _id key property back to original Id or id
//                             let propType = entityType.GetProperty(y).PropertyType
//                             if FSharpType.IsRecord propType    
//                             then rewriteKey (List.ofSeq bson.RawValue.Keys) bson propType (getKeyFieldName propType)
//                             continueToNext()
//
//                         |_, (:? BsonArray as bsonArray) ->
//                             // if property is BsonArray then loop through each element
//                             // and if that element is a record, then re-write _id back to original
//                             let collectionType = entityType.GetProperty(y).PropertyType
//                             let elementType = getCollectionElementType collectionType
//                             if FSharpType.IsRecord elementType then
//                                 let docKey = getKeyFieldName elementType
//                                 for bson in bsonArray do
//                                     if bson.IsDocument 
//                                     then
//                                       let doc = bson.AsDocument
//                                       let keys = List.ofSeq doc.RawValue.Keys
//                                       rewriteKey keys doc elementType docKey
//                             
//                             continueToNext()
//                         |_ -> 
//                             continueToNext()
//                 
//                 let keys = List.ofSeq entity.RawValue.Keys
//                 rewriteKey keys entity entityType (getKeyFieldName entityType)
//                 entity
//
//             rewriteIdentityKeys entity 
//             |> JsonSerializer.Serialize
//             |> fun json -> JsonConvert.DeserializeObject(json, entityType, converters)
//     let serializeField(any: obj) : BsonValue = 
//         // Entity => Json => Bson
//         let json = JsonConvert.SerializeObject(any, Formatting.None, converters);
//         JsonSerializer.Deserialize(json);
//
//     /// Deserializes a field of a BsonDocument to a typed entity
//     let deserializeField<'t> (value: BsonValue) = 
//         // Bson => Json => Entity<'t>
//         let typeInfo = typeof<'t>
//         value
//         // Bson to Json
//         |> JsonSerializer.Serialize
//         // Json to 't
//         |> fun json -> JsonConvert.DeserializeObject(json, typeInfo, converters)
//         |> unbox<'t>
//         
//     /// Converts a BsonDocument to a typed entity given the document the type of the CLR entity.
//     let deserialize<'t>(entity: BsonDocument) = 
//         let typeInfo = typeof<'t>
//         deserializeByType entity typeInfo
//         |> unbox<'t>
//
