module Forge.SolutionSystem

open System
open System.IO
open System.Text
open Forge
open Forge.ProjectSystem
open FParsec



type UserState = unit
type Parser<'t> = Parser<'t, UserState>

/// Sets the platform for a Build Configuration
///     x86,  x64, or AnyCPU.
/// The default is AnyCPU.


let [<Literal>] FolderGuidString = "2150E333-8FDC-42A3-9474-1A3956D46DE8"
let FolderGuid = Guid "2150E333-8FDC-42A3-9474-1A3956D46DE8"

let sectionIndent = String.replicate 4 " "
let itemIndent = String.replicate 8 " "


type PreProjectAttribute    () = inherit Attribute() 
type PostProjectAttribute   () = inherit Attribute() 
type PreSolutionAttribute   () = inherit Attribute() 
type PostSolutionAttribute  () = inherit Attribute() 

type SolutionItem = 
    { Name:string; Path:string }
    member self.ToSln() =
        sprintf "%s = %s" self.Name self.Path


type SolutionFolder = 
    {   ProjectTypeGuid     : Guid  // {2150E333-8FDC-42A3-9474-1A3956D46DE8}
        Name                : string
        Path                : string
        Guid                : Guid
        [<PreProject>]
        SolutionItems       : SolutionItem list
    }

    member self.ToSln() =
        let typeGuid = self.ProjectTypeGuid.ToString().ToUpper() 
        let idGuid = self.Guid.ToString().ToUpper()
        let header = 
            sprintf "Project(\"{%s}\") = \"%s\", \"%s\", \"{%s}\"\n" 
                typeGuid self.Name self.Path idGuid

        if self.SolutionItems = [] then header + "EndProject\n" else
        let sb = StringBuilder()

        self.SolutionItems |> List.iter (fun item -> 
            itemIndent +  item.ToSln() |> sb.AppendLine |> ignore)
        let sectionHeader = "ProjectSection(SolutionItems) = preProject\n"
        sb  .Insert(0,sectionIndent + sectionHeader)
            .AppendLine(sectionIndent + "EndProjectSection")
            .Insert(0,header) 
            .AppendLine("EndProject")
        |> string


type Project =
    {   ProjectTypeGuid     : Guid
        Name                : string
        Path                : string
        Guid                : Guid
        [<PostProject>]
        Dependecies         : Guid list
    }

    member self.ToSln() =
        let typeGuid = self.ProjectTypeGuid.ToString().ToUpper() 
        let idGuid = self.Guid.ToString().ToUpper()

        let dependencyString (guid:Guid) =
            let guidstr = guid.ToString().ToUpper()
            sprintf "{%s} = {%s}\n" guidstr guidstr

        let header = 
            sprintf "Project(\"{%s}\") = \"%s\", \"%s\", \"{%s}\"\n" 
                typeGuid self.Name self.Path idGuid

        if self.Dependecies = [] then header + "EndProject\n" else
        let sb = StringBuilder()

        self.Dependecies |> List.iter (fun guid -> 
            sectionIndent + dependencyString guid |> sb.Append |> ignore)
        let sectionHeader = "ProjectSection(ProjectDependencies) = postProject\n"
        sb  .Insert(0,sectionIndent + sectionHeader)
            .AppendLine(sectionIndent + "EndProjectSection")
            .Insert(0,header) 
            .AppendLine("EndProject")
        |> string


type SolutionConfiguration = 
    { Name:string; Platform:PlatformType }

    member self.ToSln() =
        let platfomStr = self.Platform |> function
            | AnyCPU -> "Any CPU"
            | x      -> string x
        sprintf "%s|%s = %s|%s\n" self.Name platfomStr self.Name platfomStr


type BuildProperty = 
    | ActiveCfg | Build0
    static member Parse text = text |> function
        | InvariantEqual "ActiveCfg" -> ActiveCfg
        | InvariantEqual "Build.0" -> Build0
        | _ ->
            failwithf "Could not parse '%s' into a `PlatformType`" text
    override self.ToString() = self |> function
        | ActiveCfg -> "ActiveCfg"
        | Build0    -> "Build.0"


type ProjectConfiguration =
    {   ProjectGuid   : Guid
        ConfigName    : string
        BuildProperty : BuildProperty
        Platform      : PlatformType
    }

    member self.ToSln() =
        let guidStr = self.ProjectGuid.ToString().ToUpper()
        let buildStr = string self.BuildProperty
        let platfomStr = self.Platform |> function
            | AnyCPU -> "Any CPU"
            | x      -> string x
        sprintf "{%s}.%s|%s.%s = %s|%s\n"
            guidStr self.ConfigName platfomStr buildStr self.ConfigName buildStr


type SolutionProperty = 
    { Name:string; Value:string }
    member self.ToSln() =
        sprintf "%s = %s\n" self.Name self.Value


type NestedProject = 
    { Project : Guid; Parent : Guid }
    member self.ToSln() =
        let projectGuid = self.Project.ToString().ToUpper()
        let parentGuid  = self.Parent.ToString().ToUpper()
        sprintf "{%s} = {%s}\n" projectGuid parentGuid


type Solution = 
    {   Header : string
        Folders : SolutionFolder list
        Projects : Project List
        [<PreSolution>]  SolutionConfigurationPlatforms : SolutionConfiguration list
        [<PostSolution>] ProjectConfigurationPlatforms : ProjectConfiguration list
        [<PreSolution>]  SolutionProperties : SolutionProperty list
        [<PreSolution>]  NestedProjects : NestedProject list
    }

    static member Empty = 
        {   Header  = ""
            Folders = []
            Projects = []
            SolutionConfigurationPlatforms = []
            ProjectConfigurationPlatforms = []
            SolutionProperties = []
            NestedProjects = []
        }

    member self.ToSln() =
        let sb = StringBuilder()
        sb.AppendLine self.Header |> ignore
        self.Projects |> List.iter(fun p -> p.ToSln()|>sb.Append|>ignore)
        self.Folders  |> List.iter(fun p -> p.ToSln()|>sb.Append|>ignore)
        sb.AppendLine("Global")
            .AppendLine(sectionIndent + "GlobalSection(SolutionConfigurationPlatforms) = preSolution")|> ignore
        self.SolutionConfigurationPlatforms
            |> List.iter(fun cp -> itemIndent + cp.ToSln() |> sb.Append|>ignore)
        sb.AppendLine(sectionIndent + "EndGlobalSection")
            .AppendLine(sectionIndent + "GlobalSection(ProjectConfigurationPlatforms) = postSolution")|> ignore
        self.ProjectConfigurationPlatforms
            |> List.iter(fun pp -> itemIndent + pp.ToSln() |> sb.Append|>ignore)
        sb.AppendLine(sectionIndent + "EndGlobalSection")
            .AppendLine(sectionIndent + "GlobalSection(SolutionProperties) = preSolution")|> ignore
        self.SolutionProperties
            |> List.iter(fun sp -> itemIndent + sp.ToSln() |> sb.Append|>ignore)
        sb.AppendLine(sectionIndent + "EndGlobalSection")
            .AppendLine(sectionIndent + "GlobalSection(NestedProjects) = preSolution")|> ignore
        self.NestedProjects
            |> List.iter(fun np -> itemIndent + np.ToSln() |> sb.Append|>ignore)
        sb.AppendLine(sectionIndent + "EndGlobalSection")
            .AppendLine("EndGlobal")
        |> string


//  Functionality to Add
//  
//  - Add new folder
//  - add item to folder
//  - add new project
//  - add project to folder
//  - add existing project
//  - move item inside solution
//  - remove item from solution
//  - remove project from solution
//  - remove folder from solution, recursive removal
//  - show dependency tree / build order
//  - set project build order
//  - check for cyclic dependencies 
//  - add/remove solution build configuration
//  - unload/reload project
//  - getinfo on solution item
//

(*  Notes -
    - paths in the solution file are all relative to the location of the .sln
    - folders can't have the same name as project files
    - projects must have unique names (globally)
    - files must have unique names project/folder scope
*)
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module SolutionFolder =
    
    let create name =
        {   ProjectTypeGuid = FolderGuid
            Name  = name
            Path  = name
            Guid  = Guid.NewGuid()
            SolutionItems = []
        }

    let rename name (folder:SolutionFolder) =
        { folder with Name = name; Path = name }


    let addItem name (folder:SolutionFolder) =
        if folder.SolutionItems |> List.exists 
            (fun si -> String.equalsIgnoreCase si.Name name) then
            failwithf "The Solution Folder '%s' already contains an item named '%s'" folder.Name name
        else
        { folder with
            SolutionItems = {SolutionItem.Name = name; Path=name}::folder.SolutionItems}

    let removeItem name (folder:SolutionFolder) =
        let items = 
            folder.SolutionItems |> List.filter
                (fun si -> String.equalsIgnoreCase si.Name name) 
        { folder with SolutionItems = items }






[<AutoOpen>]
module internal Parsers =
    

    type UserState = unit
    type Parser<'t> = Parser<'t, UserState>

    let ``{`` : Parser<_> = pchar '{'
    let ``}`` : Parser<_> = pchar '}'
    let ``"`` : Parser<_> = pchar '"'
    let ``(`` : Parser<_> = pchar '('
    let ``)`` : Parser<_> = pchar ')'
    let ``|`` : Parser<_> = skipChar '|'
    let ``.`` : Parser<_> = pchar '.'

    let isGuid c = isHex c || c = '-'

    let pEq     : Parser<_> = pchar '='
    let skipEqs : Parser<_> =  spaces >>. pchar '=' >>. spaces
    let skipCom : Parser<_> =  spaces >>. pchar ',' >>. spaces

    let notspace: Parser<_> = many1Satisfy ^ isNoneOf [ '\t'; ' '; '\n'; '\r'; '\u0085';'\u2028';'\u2029' ]

    let pSection : Parser<_> = pstring "Section"
    let pProject : Parser<_> = pstring "Project"
    let pEndProject : Parser<_> = pstring "EndProject" .>> notFollowedBy pSection
    let pGlobal : Parser<_> = pstring "Global"
    let pEndGlobal : Parser<_> = pstring "EndGlobal" .>> notFollowedBy pSection
    let pProjectSection : Parser<_> = pstring "ProjectSection"
    let pEndProjectSection : Parser<_> = pstring "EndProjectSection" 
    let pGlobalSection : Parser<_> = pstring "GlobalSection"
    let pEndGlobalSection : Parser<_> = pstring "EndGlobalSection"


    /// Parsers a Guid inside of { }
    let pGuid: Parser<Guid> = 
        let psr = between ``{`` ``}`` ^ manySatisfy isGuid
        fun stream  ->
            let reply: _ Reply = psr stream
            if  reply.Status <> Ok then Reply (Error,reply.Error) else
            try Guid.Parse reply.Result |> Reply 
            with ex -> Reply (Error, expected ex.Message)


    let pSolutionConfigLine =
        spaces >>. manyCharsTill anyChar ``|`` .>>. (manyCharsTill anyChar  pEq)  .>> skipRestOfLine true
        |>> fun (name, plat) -> 
        {   SolutionConfiguration.Name = name
            Platform = plat.Trim() |> PlatformType.Parse }


    let pProjectConfigLine =
        (spaces >>. pGuid .>> ``.``)
        .>>. (many1CharsTill anyChar ``|``)
        .>>. (many1CharsTill anyChar ``.``)
        .>>. (many1CharsTill anyChar ^ (spaces .>> pEq)) .>> skipRestOfLine true
        |>> fun (((guid,name),plat),prop) ->
            {   ProjectGuid = guid
                ConfigName = name
                BuildProperty = BuildProperty.Parse ^ prop.Trim()
                Platform = PlatformType.Parse  ^ plat.Trim()  }


    let pNestedProjectLine : Parser<_> =
        (spaces >>. pGuid .>> skipEqs) .>>. pGuid  .>> skipRestOfLine true
        |>> fun (proj, parent) -> { Project = proj ; Parent = parent }


    let pPair = spaces >>. notspace .>> skipEqs .>>. notspace .>> spaces

    let pPropertyLine = pPair |>> fun (n,v) -> { Name = n ; Value = v }

    /// Parses the values in the GlobalSection SolutionProperties
    let spEntries =  manyTill (spaces >>. pPropertyLine       .>> spaces) ^ lookAhead pEndGlobalSection

    /// Parses the values in the GlobalSection SolutionConfigurationPlatforms
    let scEntries =  manyTill (spaces >>. pSolutionConfigLine .>> spaces) ^ lookAhead pEndGlobalSection

    /// Parses the values in the GlobalSection ProjectConfigurationPlatforms
    let pcEntries =  manyTill (spaces >>. pProjectConfigLine  .>> spaces) ^ lookAhead pEndGlobalSection

    /// Parses the values in the GlobalSection NestedProjects
    let npEntries =  manyTill (spaces >>. pNestedProjectLine  .>> spaces) ^ lookAhead pEndGlobalSection


    /// Higher order function that takes a parser an insert/update function and an accumulator
    /// The insert function takes the Reply of the parser and the accumulator as its arguments 
    /// to produce a new accumulator which will be wrapped in a reply
    /// This is used to insert a parser into a large scale folding parser
    let inline insertBuilder psr (insertfn:'a->Reply<_>-> _) (acc:'a) : Parser<_> =
        fun stream -> 
            let reply: _ Reply = psr stream
            if reply.Status <> Ok  then Reply (Error, reply.Error) else
            insertfn acc reply |> Reply   


    let insertProperties (sln:Solution) : Parser<_> =
        sln |> insertBuilder spEntries (fun sol reply ->
            let props = List.append sol.SolutionProperties reply.Result
            { sol with SolutionProperties = props })


    let insertNestedProjects (sln:Solution) : Parser<_> =
        sln |> insertBuilder npEntries (fun sol reply ->
            let projects = List.append sol.NestedProjects reply.Result
            { sol with NestedProjects = projects })


    let insertProjectConfigs (sln:Solution) : Parser<_> =
        sln |> insertBuilder pcEntries (fun sol reply ->
            let configs =  List.append sol.ProjectConfigurationPlatforms reply.Result
            { sol with ProjectConfigurationPlatforms =  configs })


    let insertSolutionConfigs (sln:Solution) : Parser<_> =
        sln |> insertBuilder scEntries (fun sol reply ->
            let configs = List.append sol.SolutionConfigurationPlatforms reply.Result
            { sol with SolutionConfigurationPlatforms = configs })


    let sectionSwitch (sln:Solution) =
        let sectionType = between ``(`` ``)`` ^ manyCharsTill anyChar ^ lookAhead ``)``
        let switch (stream: _ CharStream) =
            let reply: _ Reply = sectionType stream
            if reply.Status <> Ok  then Reply (Error, reply.Error) else
            stream.SkipRestOfLine true // skip the rest of the line to prepare to parse the items inside the section
            match reply.Result  with
            | "ProjectConfigurationPlatforms"   -> (insertProjectConfigs  sln) stream
            | "SolutionConfigurationPlatforms"  -> (insertSolutionConfigs sln) stream
            | "SolutionProperties"              -> (insertProperties      sln) stream
            | "NestedProjects"                  -> (insertNestedProjects  sln) stream
            | s -> Reply (Error, expected <| sprintf 
                    "Inside Global Property ::\ncould not parse unexpected string -'%s'\n at Ln: %d Col: %d"
                    s stream.Line stream.Column)
        between (spaces >>. pGlobalSection) (spaces >>. pEndGlobalSection) switch


    let inline foldParser (foldfn: 'a -> Parser<'a>) (endpsr:Parser<_>) seed =
        let rec loop acc (stream: _ CharStream) =
            let state = stream.State
            let reply: _ Reply = foldfn acc ^ stream
            if reply.Status = Ok then loop reply.Result stream else
            stream.BacktrackTo state 
            let checkEnd: _ Reply = endpsr stream
            if checkEnd.Status = Ok then 
                stream.BacktrackTo state; Reply acc
            else Reply (Error, checkEnd.Error)
        loop seed


    let inline foldParserFull (foldfn: 'a -> Parser<'a>) seed =
        let rec loop acc (stream: _ CharStream) =
            let state = stream.State
            let reply: _ Reply = foldfn acc ^ stream
            if reply.Status = Ok then loop reply.Result stream 
            elif stream.IsEndOfStream then Reply acc else
            stream.BacktrackTo state 
            Reply (Error, reply.Error)
        loop seed


    let foldSections sln :Parser<_> = foldParser sectionSwitch (spaces >>. pEndGlobal) sln

    
    let parseGlobal (sln:Solution) : Parser<_> =
        between (spaces >>. pGlobal) (spaces >>. pEndGlobal) ^ foldSections sln


    let pSolutionItem = pPair |>> fun (n,v) -> { SolutionItem.Name = n ; Path = v }

    /// Parses the solution items in the ProjectSection SolutionItems
    let pFolderItems =  
        between (pProjectSection .>> skipRestOfLine true) pEndProjectSection ^
            manyTill (spaces >>. pSolutionItem  .>> spaces) ^ lookAhead pEndProjectSection


    let pProjectDependencies =
        between (pProjectSection .>> skipRestOfLine true) pEndProjectSection ^
            manyTill (spaces >>. pGuid .>> skipRestOfLine true .>> spaces) ^ lookAhead pEndProjectSection

    let quoteGuid = ``"`` >>. pGuid .>> ``"``

    let quoted: Parser<_> = between ``"`` ``"`` ^ manyCharsTill anyChar ^ lookAhead ``"``

    let projectHeading =  
        (``(`` >>. quoteGuid .>> ``)``) 
        .>>. (skipEqs >>. (quoted .>> skipCom))
        .>>. ((quoted .>> skipCom) .>>. quoteGuid .>> spaces)


    let parseProject (sln:Solution) =
        let switch (stream: _ CharStream) =
            let reply = projectHeading stream
            if reply.Status <> Ok then Reply (Error, reply.Error) else
            let (typeGuid,name),(path,idGuid) = reply.Result
            let state = stream.State
            if typeGuid = FolderGuid then
                let solutionFolder =
                    {   ProjectTypeGuid = typeGuid                    
                        Name = name
                        Path = path
                        Guid = idGuid
                        SolutionItems = []
                    }
                let reply = pFolderItems stream
                if reply.Status <> Ok then 
                    stream.BacktrackTo state
                    { sln with Folders = solutionFolder::sln.Folders } |> Reply
                else
                    let subFolders = reply.Result
                    let solutionFolder = {solutionFolder with SolutionItems = subFolders }
                    { sln with Folders = solutionFolder::sln.Folders } |> Reply
            else
                let project =
                    {   ProjectTypeGuid = typeGuid
                        Name = name
                        Path = path
                        Guid = idGuid
                        Dependecies = []
                    }
                let reply = pProjectDependencies stream
                // if we don't find any dependencies for the project we backtrack
                if reply.Status <> Ok then 
                    stream.BacktrackTo state
                    { sln with Projects = project::sln.Projects} |> Reply
                else
                    let dependencyIds = reply.Result
                    let project = { project with Dependecies = dependencyIds}
                    { sln with Projects = project::sln.Projects} |> Reply
        between (spaces >>. pProject) (spaces >>. pEndProject) switch


    let pSolutionHeader = manyCharsTill anyChar ^ lookAhead ^ (pProject <|> pGlobal)
       

    let parseSolution (sln:Solution) : Parser<_> =
        let entry sln = spaces >>. choice [parseGlobal sln; parseProject sln ]
        let parseEntries = spaces >>. foldParser entry (spaces >>. eof) sln
        fun stream ->
            let reply = pSolutionHeader stream
            if reply.Status <> Ok then Reply (Error, reply.Error) else
            let header = reply.Result
            let reply = parseEntries stream
            if reply.Status <> Ok then Reply (Error, reply.Error) else
            let parsedSln = reply.Result
            { parsedSln with Header = header } |> Reply
