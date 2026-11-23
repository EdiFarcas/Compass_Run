# Integrare edi + elina1 — 23 noiembrie 2026

Main remote a fost actualizat de la 3c9aa71 la 856f85f. Integrarea edi (8b69c4e) a fost publicată în main prin be609b6. Conflictul scenei a fost rezolvat păstrând managerul de ceață, skybox-ul zi/noapte și ambele obiecte rădăcină.

Branch-ul local integration/elina1-test integrează origin/elina1 la e4d5b1c peste acel main. Nu a fost publicat. Merge-ul elina1 nu a produs conflicte textuale.

## Corecții de integrare

- Legat prefab-urile noi de copaci, tufe, stânci, apă și iarbă în SimulareVR; branch-ul elina1 aducea codul și asset-urile fără aceste referințe în scena principală.
- Corectat scala Terrain de la (100, 0, 100) la (1, 1, 1).
- Păstrat dependențele vechi de modele/texturi și GUID-urile lor: scenele/prefab-urile LocalReview și ferigile încă le folosesc. Scena principală utilizează copacii noi.
- Corectat shader-ul Nyxis: calculul ceții folosește adâncimea clip din vertex, nu adâncimea dispozitivului din fragment. Iarba respectă și culoarea/intensitatea luminii soarelui/lunii.

## Validare

Unity 6000.6.4f1, macOS Metal, scena SimulareVR, Play Mode. Două rulări ale verificărilor runtime, a doua după corecția shader-ului. Fără erori C# sau shader noi în logul verificat.

PASS: generated route and five keypoints
PASS: trees generated: 617
PASS: lake water generated
PASS: instanced grass generated: 15 batches
PASS: no missing scripts in active scene
PASS: whistle configured and idle
PASS: whistle Activate plays sound
PASS: spray Activate emits particles
PASS: spray Deactivate stops emission
PASS: compass needle exists
PASS: compass north yaw 0
PASS: compass north yaw 45
PASS: compass north yaw 90
PASS: compass north yaw 180
PASS: compass north yaw 270
PASS: day-night cycle retained
PASS: daytime sun
PASS: nighttime moon
PASS: three checkpoint sockets
PASS: CP1 validates ring
PASS: fog retained and linked to checkpoints
Runtime validation finished in 4.0 seconds


Verificat vizual: vegetația/stâncile noi sunt afișate, iar iarba este verde după corectarea ceții.

Limite: evenimentele Activate/Deactivate și validarea CP1 au fost invocate programatic. Aceste verificări nu înlocuiesc parcurgerea manuală cu XR Device Simulator sau testarea pe cască. Rămâne de verificat manual apucarea/eliberarea inelului în socket, mersul pe traseu, ceața CP1–CP2, lacurile și performanța pe calculatorul utilizatorului.

Scriptul temporar de validare a fost mutat în Backups/IntegrationValidation-2026-11-23, în afara Assets, pentru a nu porni automat în proiect. Schimbările locale de dinaintea integrării (teren, material spray, soluție) sunt păstrate în stash-ul „Preserve local Unity generated changes before branch integration 2026-11-23”; nu au fost reaplicate peste terenul nou.
