# Activarea fluierului și spray-ului prin trigger

Implementat în `Assets/LocalReview/SimulareVR_Local.unity`. Scena originală a echipei și scripturile ei nu au fost modificate. Nu este necesar un script runtime pentru aceste efecte: legăturile sunt persistente, vizibile în Inspector, în componenta XR Grab Interactable.

## 1. Fluierul

1. În Hierarchy: deschide `Start`, apoi selectează obiectul `Fluier` care are componenta `XR Grab Interactable` (nu mesh-ul copil cu același nume).
2. Am creat sub el un copil numit `Fluier_Sunet`, poziționat la centrul mesh-ului.
3. Pe copil am adăugat un `AudioSource` cu:
   - Audio Clip: `Assets/LocalReview/Effects/Fluier.wav` (sunet de fluier sintetizat, mono, 0,9 s).
   - Play On Awake: oprit.
   - Loop: oprit.
   - Volume: 0,4.
   - Spatial Blend: 1 / complet 3D.
   - Doppler Level: 0.
   - Volume Rolloff: Linear; Min Distance 0,5 m; Max Distance 15 m.
4. Pe obiectul părinte `Fluier`, am deschis `XR Grab Interactable > Interactable Events > Activated`.
5. Am adăugat o intrare cu `+`, având ținta `Fluier_Sunet (AudioSource)` și funcția `AudioSource.Play()` fără parametri. Starea intrării este `Runtime Only`.
6. Nu am conectat Deactivated la fluier: o apăsare redă clipul complet; o nouă apăsare îl repornește.

Pentru alt sunet, înlocuiește câmpul Audio Clip. Pentru volum, schimbă Volume. Pentru un fluier continuu cât ții trigger-ul: bifează Loop și conectează și Deactivated la `AudioSource.Stop()`.

## 2. Spray-ul

1. În Hierarchy: `Start > Spray_Ursi`.
2. Am creat copilul `Spray_Fum` și i-am adăugat un `Particle System`.
3. Am poziționat emisia lângă capacul/duza cu materialul negru și am orientat axa Z a particulelor spre exterior. Transform local rezultat: Position aproximativ `(0, 0,02, -0,05)`, Rotation `(270, 0, 0)`. Poți ajusta aceste valori direct pe copil dacă schimbi modelul.
4. În modulul Main:
   - Duration 1 s; Looping pornit; Play On Awake oprit.
   - Start Lifetime random între 0,4 și 0,8 s.
   - Start Speed random între 0,8 și 1,6 m/s.
   - Start Size random între 0,035 și 0,07 m.
   - Start Color portocaliu: RGBA `(1, 0,35, 0,06, 0,65)`.
   - Simulation Space: World, astfel încât particulele deja emise să nu se deplaseze odată cu recipientul.
   - Max Particles: 250.
5. Emission: Rate over Time 55 particule/s.
6. Shape: Cone, Angle 12°, Radius 0,006 m.
7. Color over Lifetime: alb, cu alpha 0 la început, 0,8 la 12% din viață și 0 la final, pentru apariție și dispariție graduală.
8. Size over Lifetime: creștere de la factor 0,5 la 2,5.
9. Noise: activat; Strength 0,1; Frequency 1; Scroll Speed 0,4; Quality Low.
10. Renderer: Billboard, fără umbre. Material `Effects/Spray_Fum_Orange.mat`, shader `Universal Render Pipeline/Particles/Unlit`, transparent, cu textura radială moale `Effects/FumSoft.png`.
11. Pe `Spray_Ursi > XR Grab Interactable > Interactable Events`, am conectat:

| Eveniment | Țintă | Funcție |
|---|---|---|
| Activated | Spray_Fum / Particle System | ParticleSystem.Play() |
| Deactivated | Spray_Fum / Particle System | ParticleSystem.Stop() |
| Select Exited | Spray_Fum / Particle System | ParticleSystem.Stop() |

Toate intrările sunt `Runtime Only`. Stop oprește emisia; particulele deja emise dispar natural. Select Exited previne emiterea în continuare când jucătorul lasă obiectul din mână în timp ce ține trigger-ul.

Pentru altă culoare: Main > Start Color. Pentru mai mult fum: Emission > Rate over Time. Pentru dispersie: Shape > Angle. Pentru distanța jetului: Start Speed și Start Lifetime. Pentru direcție: rotește copilul Spray_Fum.

## 3. Testare cu simulatorul din laborator

1. Deschide scena locală și apasă Play; Pause trebuie să fie oprit. Click în Game.
2. Selectează mâna stângă cu T sau mâna dreaptă cu Y. Alternativ ține Shift, respectiv Space.
3. Apropie controlerul de obiect sau îndreaptă raza spre el.
4. Ține G pentru grip/apucare.
5. Cu obiectul încă ținut, apasă click stânga: acesta simulează trigger-ul controllerului selectat. Pentru spray, ține apăsat click stânga.
6. Eliberează click-ul: spray-ul nu mai emite. Eliberează G: obiectul este lăsat și spray-ul se oprește de asemenea.

Dacă fluierul nu se aude: verifică Volume, Audio Clip, Game view > Mute Audio, sunetul sistemului și existența unui AudioListener activ pe camera XR.

## 4. Verificarea efectuată

În Play Mode am declanșat programatic callback-urile XR Activate/Deactivate și evenimentul Select Exited, folosind legăturile salvate în scenă. Rezultate: efectele sunt oprite inițial; AudioSource.isPlaying devine true la activarea fluierului; spray-ul emite particule la activare; Deactivated și Select Exited opresc emisia. Acest test verifică funcționarea evenimentelor, nu înlocuiește un test manual al trigger-ului pe o cască reală.

Unelte editor pentru reproducere și verificare: `Tools > Compass Run > Setup Whistle and Spray Effects` și `Test Activation Effects (Play Mode)`. Prima configurare este deja aplicată. Aceste unelte nu rulează automat la deschiderea proiectului.
