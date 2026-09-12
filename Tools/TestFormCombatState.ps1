$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$source=Get-Content -Raw -LiteralPath (Join-Path $root 'Assets/Scripts/Assembly-CSharp/Model.cs')
$methods=[regex]::Match($source,'(?s)    internal System.Action TransferFormCombatState.*?(?=\tpublic ModelController DEGJJOMLJGM)').Value
if(!$methods){throw 'Combat state extraction failed.'}
$fixture=Join-Path $root ('Temp/FormCombatState-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture | Out-Null
foreach($file in @('ModelController.cs','KeyData.cs','FightCID.cs')) {
 Copy-Item -LiteralPath (Join-Path $root "Assets/Scripts/Assembly-CSharp/$file") -Destination $fixture
}
$code=@'
using System;
using System.Collections.Generic;
using System.Linq;
public class EventDispatcher<T>{readonly Dictionary<int,Action<T>> events=new Dictionary<int,Action<T>>();public void AddEventListener(int key,Action<T> action){events.TryGetValue(key,out var previous);events[key]=previous+action;}public void CallEvent(int key,T data){if(events.TryGetValue(key,out var action))action(data);}}
static class Extensions{public static bool ANNPHPHLNEH<T>(this List<T> a,List<T>b)=>a.SequenceEqual(b);}
class Stats {public Model Owner;public int Hits;public void RebindFormOwner(Model model){Owner=model;}}
class Model {
METHODS
ModelController FEHOHLMIEBP=new ModelController();
Stats _Statistics=new Stats(),DKFGOHCNIKL=new Stats();
object MDFEHKBOHEL=new object();bool HCPHOJKFIDM;
int JMHJDHLBHLK,LGLIHLJPDIO,DJOKGDICHAJ,AIAKAAECMEH,AAEFMEJBMLH,PACHBHGEIGN;
static void Check(bool value,string message){if(!value)throw new Exception(message);}
static void Main(){
 var old=new Model();var next=new Model();
 old.DKFGOHCNIKL.Owner=old;next.DKFGOHCNIKL.Owner=next;
 old._Statistics.Hits=17;old.DKFGOHCNIKL.Hits=29;
 old.HCPHOJKFIDM=true;old.JMHJDHLBHLK=2;old.LGLIHLJPDIO=3;old.DJOKGDICHAJ=4;old.AIAKAAECMEH=5;old.AAEFMEJBMLH=6;old.PACHBHGEIGN=7;
 int oldEvents=0,newEvents=0;
 old.FEHOHLMIEBP.AddEventListener(0,_=>oldEvents++);old.FEHOHLMIEBP.AddEventListener(1,_=>oldEvents++);
 next.FEHOHLMIEBP.AddEventListener(0,_=>newEvents++);next.FEHOHLMIEBP.AddEventListener(1,_=>newEvents++);
 old.FEHOHLMIEBP.OnPressAnyKey(3);
 for(int i=0;i<17;i++)old.FEHOHLMIEBP.Render();
 var keys=old.FEHOHLMIEBP.ANALKHBJKIO();var cooldown=old.MDFEHKBOHEL;
 var undo=old.TransferFormCombatState(next);
 Check(next.FEHOHLMIEBP.ANALKHBJKIO()==keys&&keys.CEPODJDDLBF.Contains(3),"held input and buffer identity");
 Check(oldEvents==1&&newEvents==0,"transfer emits no input");
 Check(next._Statistics.Hits==17&&next.DKFGOHCNIKL.Hits==29&&next.DKFGOHCNIKL.Owner==next,"history and owner");
 Check(old.DKFGOHCNIKL.Owner==old&&next.MDFEHKBOHEL==cooldown,"retired owner and cooldown");
 Check(next.HCPHOJKFIDM&&next.JMHJDHLBHLK==2&&next.LGLIHLJPDIO==3&&next.DJOKGDICHAJ==4&&next.AIAKAAECMEH==5&&next.AAEFMEJBMLH==6&&next.PACHBHGEIGN==7,"control, round and counters");
 undo();undo();
 Check(old.FEHOHLMIEBP.ANALKHBJKIO()==keys&&old._Statistics.Hits==17&&old.DKFGOHCNIKL.Owner==old&&old.MDFEHKBOHEL==cooldown,"idempotent restore");
 old.TransferFormCombatState(next);
 next.FEHOHLMIEBP.OnReleaseAnyKey(3);
 Check(newEvents==1&&oldEvents==1&&!keys.CEPODJDDLBF.Contains(3),"release goes to replacement subscribers only");
 next.FEHOHLMIEBP.OnPressAnyKey(9);
 Check(newEvents==2&&oldEvents==1,"new press reaches new body");
 // Compare uninterrupted input with a transferred controller at every tick.
 var control=new ModelController();var moving=new ModelController();var destination=new ModelController();
 control.OnPressAnyKey(9);moving.OnPressAnyKey(9);
 for(int i=0;i<11;i++){control.Render();moving.Render();}
 moving.ExchangeFormInput(destination);
 for(int i=0;i<40;i++){control.Render();destination.Render();Check(control.ANALKHBJKIO().IGEEOAGOMEM.SequenceEqual(destination.ANALKHBJKIO().IGEEOAGOMEM)&&control.ANALKHBJKIO().CEPODJDDLBF.SequenceEqual(destination.ANALKHBJKIO().CEPODJDDLBF),"combo expiry retains original timing");}
 Console.WriteLine("PASS: production combat-state transfer and complete native controller/key data; held release, subscriber isolation, uninterrupted combo timing, history/cooldowns/counters, rollback. Statistics storage and event dispatcher controlled.");
}
}
'@
[IO.File]::WriteAllText((Join-Path $fixture 'Program.cs'),$code.Replace('METHODS',$methods))
[IO.File]::WriteAllText((Join-Path $fixture 'Test.csproj'),'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>')
dotnet run --project (Join-Path $fixture 'Test.csproj') --verbosity quiet
if($LASTEXITCODE -ne 0){throw 'Form combat state checks failed.'}
