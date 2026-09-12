$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$source=Get-Content -Raw -LiteralPath (Join-Path $root 'Assets/Scripts/Assembly-CSharp/DistancePoint.cs')
$fields=[regex]::Match($source,'(?s)public class DistancePoint\s*\{(.*?)\tpublic DistancePoint\(\)').Groups[1].Value
$methods=foreach($name in @('UpdateNode','MHIDGNCKHON','PAPCNMHMBOO')){
 $match=[regex]::Match($source,"(?ms)^\t(?:public|protected|private) (?:void|PointNode) $name\(.*?^\t\}")
 if(!$match.Success){throw "Native cache method missing: $name"};$match.Value
}
if(!$fields){throw 'Native cache fields missing.'}
$fixture=Join-Path $root ('Temp/AnimationNodeRebind-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture | Out-Null
$code=@'
using System;
using System.Collections.Generic;
class ModelNode{}
class ModelObject{public bool RequireCompleteNodeBindings;public ModelNode Node=new ModelNode();public ModelNode EGHIDHMENEF(string name)=>name=="foot"?Node:null;}
class ModelType{public enum KEIDBIOIFGA{MODEL_NULL,MODEL_THIS,MODEL_OTHER,MODEL_OTHER_CHILD,MODEL_PARENT}}
class ModelConditions{public bool FDELMAHAAJD,IsPlayer;public Position IHJJBIDMEMB=new Position();public class Position{public ModelObject CBAECAAKAIA;}}
static class LLLOJBFMONN{public static void Error(string s,params object[] args){throw new Exception(s);}}
class DistancePoint{
 FIELDS
 METHODS
 static void Check(bool x,string why){if(!x)throw new Exception(why);}
 public static void Main(){
  var p=new DistancePoint{HLGJJGHDEAP=JJIAEPLMBFF.OBJECT_NODES,Part="foot",OOFFOILONLO=ModelType.KEIDBIOIFGA.MODEL_THIS};
  var left=new ModelObject();var right=new ModelObject();var next=new ModelObject();var pivot=new ModelNode();
  var l=new ModelConditions{IsPlayer=true};var r=new ModelConditions{IsPlayer=false};
  p.UpdateNode(left,true,pivot,false,left);p.UpdateNode(right,false,null,false,right);
  Check(p.MHIDGNCKHON(l).Node==left.Node&&p.MHIDGNCKHON(r).Node==right.Node,"initial sides");
  p.UpdateNode(next,true,null,false,next);
  Check(p.MHIDGNCKHON(l).Node==next.Node&&p.MHIDGNCKHON(r).Node==right.Node,"replacement changes only its side");
  p.OOFFOILONLO=ModelType.KEIDBIOIFGA.MODEL_OTHER;
  Check(p.MHIDGNCKHON(r).Node==next.Node&&p.MHIDGNCKHON(l).Node==right.Node,"opponent lookup follows new side");
  p.UpdateNode(left,true,pivot,false,left);
  Check(p.MHIDGNCKHON(r).Node==left.Node&&p.MHIDGNCKHON(r).CHEKEGGJDBL==pivot,"old node and pivot restored");
  var child1=new ModelObject();var child2=new ModelObject();
  p.UpdateNode(child1,true,null,true,child1);p.UpdateNode(child2,true,null,true,child2);
  p.OOFFOILONLO=ModelType.KEIDBIOIFGA.MODEL_THIS;l.FDELMAHAAJD=true;l.IHJJBIDMEMB.CBAECAAKAIA=child1;
  Check(p.MHIDGNCKHON(l).Node==child1.Node,"child identity 1");l.IHJJBIDMEMB.CBAECAAKAIA=child2;Check(p.MHIDGNCKHON(l).Node==child2.Node,"child identity 2");
  p.UpdateNode(next,true,null,false,next);Check(p.MHIDGNCKHON(l).Node==child2.Node,"main form change does not replace child cache");
  l.FDELMAHAAJD=false;
  p.UpdateNode(new ModelObject{Node=null},true,null,false,null);Check(p.MHIDGNCKHON(l).Node==null,"native missing-node update is silent");
  p.UpdateNode(left,true,pivot,false,left);Check(p.MHIDGNCKHON(l).Node==left.Node,"restore after missing node");
  bool rejected=false;try{p.UpdateNode(new ModelObject{Node=null,RequireCompleteNodeBindings=true},true,null,false,null);}catch(InvalidOperationException e){rejected=e.Message.Contains("foot");}
  Check(rejected&&p.MHIDGNCKHON(l).Node==left.Node&&p.MHIDGNCKHON(l).CHEKEGGJDBL==pivot,"strict missing node rejects before replacing cached node/pivot");
  Console.WriteLine("PASS: production DistancePoint cache fields/update/lookup; player/opponent rebinding, reverse restoration, pivot and child identity isolation. Missing native nodes silently bind null; complete rig validation remains required.");
 }
}
'@
$code=$code.Replace('FIELDS',$fields).Replace('METHODS',($methods -join "`n"))
[IO.File]::WriteAllText((Join-Path $fixture 'Program.cs'),$code)
[IO.File]::WriteAllText((Join-Path $fixture 'Test.csproj'),'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><NoWarn>CS0649</NoWarn></PropertyGroup></Project>')
dotnet run --project (Join-Path $fixture 'Test.csproj') --verbosity quiet
if($LASTEXITCODE -ne 0){throw 'Native animation cache checks failed.'}
