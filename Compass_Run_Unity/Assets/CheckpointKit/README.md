# Checkpoint XR Socket — fișiere pentru integrare

Testat cu Unity 6000.6.4f1, URP și XR Interaction Toolkit 3.6.1.

## Instalare pe calculatorul colegului

1. Fă commit/backup la munca locală a proiectului colegului.
2. Dezarhivează ZIP-ul în rădăcina proiectului Unity, acolo unde sunt Assets, Packages și ProjectSettings. Rezultatul trebuie să fie `Assets/CheckpointKit/...`. Copiază și toate fișierele `.meta`; nu le regenera.
3. Pachetul necesită XR Interaction Toolkit 3.6.1 și URP deja instalate. Nu înlocuiește Packages sau ProjectSettings și nu adaugă tag-uri/interaction layers globale.
4. Așteaptă compilarea Unity.
5. Din `Assets/CheckpointKit/Prefabs`, pune în scenă `CheckpointSocket.prefab` și `CheckpointRing.prefab`.
6. Înlocuiește doar inelul utilizat în test cu noul prefab sau dezactivează temporar vechiul inel. Nu lăsa două inele suprapuse. Un inel vechi fără componenta CheckpointRing va fi refuzat.
7. Pentru generarea automată: pe obiectul TrailGenerator atribuie `CheckpointSocket.prefab` în câmpul `Checkpoint Prefab`. Nu este necesară schimbarea codului TrailGenerator.cs. Fiecare clonă CP1/CP2/CP3 are propria validare.
8. Salvează scena de integrare aleasă de echipă și fă commit cu folderul CheckpointKit și scena modificată de coleg. Nu este nevoie de Library, Temp sau fișierele de test de pe calculatorul autorului.

## Ce face mecanica

- Un SphereCollider trigger cu raza 0,18 m detectează apropierea inelului.
- RingSocketInteractor este un XR Socket Interactor specializat: acceptă doar un obiect cu CheckpointRing și Ring Id `CompassRunRing`.
- Preia inelul chiar dacă este încă ținut de controller, prin API-ul normal XR SelectExit/SelectEnter. Socket-ul standard nu preia automat obiectele deja selectate, de aceea există această specializare.
- Protecția din CheckpointRing previne reapucarea accidentală de către mâna al cărei grip este încă apăsat după transfer. Eliberează grip-ul și apasă din nou pentru a recupera inelul.
- Inelul se fixează la SocketAttach, fără întârziere de atașare. Socket-ul conține un singur inel și nu fură un inel deja instalat în alt socket.
- La prima introducere, CheckpointObjective.IsCompleted devine true, indicatorul devine verde, consola afișează `Checkpoint validat: CP1` (numele obiectului părinte) și se execută On Validated.
- Scoaterea inelului nu anulează un obiectiv deja validat. Reinserarea nu declanșează din nou evenimentul.
- Poți lua inelul pentru checkpoint-ul următor. Cât îl scoți, socket-ul anterior nu îl recapturează până când acesta iese din zona de capturare. Pentru o nouă încercare, apelează ResetObjective și scoate/reintrodu inelul.

## Componente și locul lor în Inspector

`CheckpointSocket` (rădăcină, 1,5 m înălțime)
- `CheckpointVisual`: modelul cu materiale pe ambele fețe și coliziune statică.
- `RingSocket`: SphereCollider cu Is Trigger, RingSocketInteractor și CheckpointObjective.
  - `SocketAttach`: poziția și rotația finală a inelului.
  - `ValidationIndicator`: indicator albastru -> verde.

`CheckpointRing`
- Rigidbody, BoxCollider, XRGrabInteractable și CheckpointRing.
- `RingVisual`: mesh-ul inelului, diametru aproximativ 12 cm.
- `RingAttach`: reperul inelului aliniat cu SocketAttach.

Nu adăuga un al doilea XR Socket Interactor standard pe același obiect: RingSocketInteractor îl extinde deja.

## Legarea la progres / UI / sunet

Selectează `CheckpointSocket > RingSocket > Checkpoint Objective > On Validated`, apasă +, trage obiectul cu managerul/UI-ul și alege metoda lui fără parametri. Evenimentul se execută o singură dată la prima introducere corectă.

Pentru manager în cod: folosește `CompassRun.Checkpoints.CheckpointObjective`, proprietatea IsCompleted și evenimentul OnValidated. Dacă scriptul managerului este într-un asmdef propriu, adaugă referința `CompassRun.Checkpoints` în acel asmdef.

Acest pachet validează fiecare checkpoint individual. Nu impune ordinea CP1 -> CP2 -> CP3 și nu implementează scorul sau victoria; managerul echipei poate folosi evenimentul OnValidated pentru acestea.

## Ce poți modifica

- Raza magnetului: RingSocket > SphereCollider > Radius.
- Punctul și orientarea fixării: SocketAttach > Transform. Axa și rotația lui RingAttach se aliniază cu SocketAttach.
- Înălțimea socket-ului: RingSocket > Local Position (implicit 0, 1.1, 0.12).
- Preluarea din mână: RingSocketInteractor > Take From Controller.
- Inelul permis: Accepted Ring Id pe socket și Ring Id pe inel trebuie să coincidă.
- Culorile: CheckpointObjective > Waiting Color / Completed Color.
- Identificatorul: CheckpointObjective > Checkpoint Id; gol folosește numele părintelui, inclusiv numele CP1/CP2/CP3 date de generator.

## Test cu simulatorul laboratorului

Play -> click în Game. T/Y selectează controllerul stâng/drept; Shift/Space sunt alternative cât sunt ținute. Ține G pentru grip, apropie inelul de indicator: se transferă în socket și indicatorul devine verde. Eliberează G, apoi apasă G din nou și îndepărtează inelul pentru a-l folosi la alt checkpoint. Încearcă un fluier sau un spray: sunt refuzate.

## Fișierele livrate

- Runtime/CheckpointRing.cs — identitatea inelului și protecția la grip.
- Runtime/RingSocketInteractor.cs — filtrare, transfer din mână și recuperare fără recapturare imediată.
- Runtime/CheckpointObjective.cs — validare unică, indicator și UnityEvent.
- Runtime/CompassRun.Checkpoints.asmdef — assembly separată.
- Prefabs/CheckpointSocket.prefab și Prefabs/CheckpointRing.prefab.
- Meshes/ și Materials/ — copii noi ale geometriei/materialelor necesare prefab-urilor; nu depind de FBX-urile originale.
- README.md, TEST_RESULTS.txt și toate fișierele .meta.

Nu au fost modificate scena comună, TrailGenerator.cs, modelele originale sau setările proiectului. Nu s-a făcut push.
