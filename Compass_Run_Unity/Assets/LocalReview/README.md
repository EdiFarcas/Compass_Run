# Test local fără modificarea scenei echipei

Deschide `Assets/LocalReview/SimulareVR_Local.unity`, apoi Play și click în Game. Butonul Pause trebuie să fie oprit.

- Ține Shift pentru controlerul stâng, Space pentru cel drept; T/Y selectează persistent mâna.
- Mouse și WASD, Q/E deplasează dispozitivul; R comută între deplasare și rotație.
- G apucă obiectul vizat, eliberarea tastei îi dă drumul. Click stânga este trigger.
- Click dreapta manipulează capul. Backslash comută blocarea cursorului.

## Corecții numai în această copie

- Terrain scale: (100, 0, 100) -> (1, 1, 1). Scara Y=0 turtea terenul.
- Copie separată de TerrainData pentru a izola generarea și vopsirea terenului.
- Prefab local de arbore cu LODGroup; feriga și iarba nu mai sunt folosite ca prototipuri de arbori.
- 35 arbori pentru testare pe laptop, distanță de afișare 90 m; seed 11102026 repetabil.
- Luminile importate în obiectele de start sunt oprite în copia scenei.

## Verificare efectuată

Play Mode: 5 puncte de traseu, 12 segmente, 32 arbori plasați pentru seed-ul de test; rig-ul pe suprafața terenului; cinci obiecte și două controlere; zero scripturi lipsă. Test programatic XR: toate cele 10 perechi controler/obiect au permis selectare și eliberare. După rularea fizicii, obiectele rămân pe masă.

## Ce rămâne de implementat în echipă

- Validarea vizitării CP1 -> CP2 -> CP3 și finalizarea jocului: momentan generatorul doar plasează obiecte, fără logică de progres/scor.
- Generatorul are finishPrefab, startGatePrefab, waterPrefab și obstacole neatribuite.
- Build Settings conține SampleScene, nu scena VR; pentru build trebuie aleasă explicit scena finală de echipă.
- TerrainData este modificat direct de TrailGenerator.Generate; în varianta finală se recomandă o copie runtime înainte de generare.
- Modelele de vegetație sunt foarte detaliate; optimizarea LOD și densității trebuie decisă înainte de testarea pe cască.

Scena originală SimulareVR și scripturile colegilor nu au fost modificate pentru aceste corecții. Toate fișierele de lucru sunt sub Assets/LocalReview. Nu a fost făcut push.
