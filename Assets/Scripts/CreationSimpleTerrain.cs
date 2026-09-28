using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;

using static CreationSimpleTerrain;
using System.Security.Cryptography;


[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshCollider))]


public class CreationSimpleTerrain : MonoBehaviour
{
    [Range(1, 2000)]
    public float dimension = 100;
    [Range(1, 15)]
    [Tooltip("La resolution sera 2 élévé à la puissance de cette valeur 2^n ")]
    public int puissance2Resolution = 1;
    public bool CentrerPivot = true;
    [HideInInspector] public ushort resolution;


    [Header("Paramètres Sinusoïde (vallée, globale)")]
    public float amplitudeSinus = 5f;
    public float frequenceSinus = 0.1f;

    [Header("Paramètres Colline (gaussienne, locale)")]
    public float hauteurColline = 20f;
    public float rayonColline = 20f;

    [Header("Paramètres Perlin (relief, global)")]
    public float amplitudePerlin = 15f;
    public float echellePerlin = 0.05f;

    [Header("Paramètres HeightMap")]
    public float amplitudeTexture = 30f;



    [Tooltip("Information sur le mode de génération :  ")]
    public enum ChoixModeDeformation
    {
        Fonction,
        Texture
    }
    public ChoixModeDeformation choixModeDeformation;
    public enum TypeFonction
    {
        Sinusoide,
        Collines,
        Perlin
    }
    [Tooltip("Choix de la première fonction à appliquer ")]
    public TypeFonction typeFonction;


    [Tooltip("Choix de la première HeightMap à appliquer ")]
    public int numTexture;
    [Tooltip("Les HeightMaps disponibles ")]
    public List<Texture2D> textures;




    private uint p_dimVertices;
    private uint p_dimTriangles;

    private MeshCollider p_meshCollider;
    private MeshFilter p_meshFilter;
    private Mesh p_mesh;
    private Vector3[] p_vertices;
    private Vector3[] p_normals;
    private int[] p_triangles;



    private Camera p_cam;
    private LayerMask maskPickingTerrain;

    private float p_dimInterVertices;


    // ==================== AJOUTS points 7 à 13 ====================

    // ---- Point 10 : affichage des normales (F10 maintenu 3s) ----
    public enum ModeAffichageNormales
    {
        Aucune,
        NormalesVertices,
        NormalesEclairage,
        NormaleOrientation,
        OrientationEtEclairage
    }
    [HideInInspector] public ModeAffichageNormales modeAffichageNormales = ModeAffichageNormales.Aucune;

    private float p_dureeAppuiF10 = 0f;
    private bool p_f10DejaDeclenche = false;
    private const float delaiAppuiLongF10 = 3f;
    private Material p_materialLignes;
    private const float longueurAffichageNormales = 3f;

    // ---- Point 11 : F11 appuyé 2 fois consécutives ----
    private float p_dernierAppuiF11 = -10f;
    private const float delaiDoubleAppuiF11 = 0.4f;

    // ---- Point 13 : fenêtre d'aide (F1) ----
    private bool p_afficherAide = false;
    private float p_fps = 0f;


    // cette méthode est appelée la première fois que le component est ajouté à l'objet (ou quand reset demandé sur le component depuis l'inspector)
    // equivalent du RequireComponent mais plus de possibilité (plusieurs components) ou intialisation automatique des paramètres public 
    void Reset()
    {
        if (GetComponent<MeshFilter>() == null)
            EditorUtility.DisplayDialog("Le component MeshFilter est nécessaire à la sélection", "MeshCollider ajouté au gameObject ! ", "j'ai compris ! ");
        //else        {
        //    EditorUtility.DisplayDialog("MeshFilter initial supprimé avant génération procédurale ! ", "Le script génére un mesh procédural . Le mesh existant " + GetComponent<MeshFilter>().mesh.name + " a été disocié de  l'objet " + gameObject.name, "j'ai compris ! ");
        //    DestroyImmediate(GetComponent<MeshFilter>());
        //}

        gameObject.AddComponent<MeshFilter>();

        Collider[] colliders = GetComponents<Collider>();
        if (colliders.Length > 0)
        {
            foreach (Collider collider in colliders)
                DestroyImmediate(collider);
            EditorUtility.DisplayDialog("Un seul MeshCollider", "Tous les collider sont supprimés ; seuls un meshCollider est associé au terrain", "OK ! ");
        }
        else
            EditorUtility.DisplayDialog("Le component MeshCollider est nécessaire à la sélection", "MeshCollider ajouté au gameObject ! ", "j'ai compris ! ");
        p_meshCollider = gameObject.AddComponent<MeshCollider>();

        if (GetComponent<MeshRenderer>() == null)
        {
            EditorUtility.DisplayDialog("Le component MeshRenderer est nécessaire à la sélection", "MeshCollider ajouté au gameObject ! ", "j'ai compris ! ");
            gameObject.AddComponent<MeshRenderer>();
        }

    }


    void Awake()
    {
        p_cam = Camera.main;

        // les rayCast exploiteront cette layer pour ne tester la collision qu'avec l'objet terrain
        gameObject.layer = LayerMask.NameToLayer("L_PickingTerrain");
        maskPickingTerrain = LayerMask.GetMask("L_PickingTerrain");

        p_meshFilter = GetComponent<MeshFilter>();
        p_meshCollider = GetComponent<MeshCollider>();

        creerMaterialLignes();
    }





    private void appliquerDeformation_Fonction()
    {
        for (int i = 0; i < p_vertices.Length; i++)
        {
            float x = p_vertices[i].x;
            float z = p_vertices[i].z;
            float y = 0f;

            switch (typeFonction)
            {
                case TypeFonction.Sinusoide:
                    y = amplitudeSinus * Mathf.Sin(frequenceSinus * x) * Mathf.Cos(frequenceSinus * z);
                    break;

                case TypeFonction.Collines:
                    float d2 = x * x + z * z; // distance au centre au carré
                    y = hauteurColline * Mathf.Exp(-d2 / (2f * rayonColline * rayonColline));
                    break;

                case TypeFonction.Perlin:
                    y = amplitudePerlin * Mathf.PerlinNoise(x * echellePerlin, z * echellePerlin);
                    break;
            }

            p_vertices[i] = new Vector3(x, y, z);
        }

        miseAJourMeshApresDeformation();

    }


    private bool appliquerDeformation_Texture()
    {
        if (textures == null || textures.Count == 0) return false;

        Texture2D tex = textures[numTexture % textures.Count];
        if (tex == null)
        {
            Debug.LogError("HeightMap : la texture est vide dans la liste Textures.");
            return false;
        }
        // GetPixelBilinear lève une exception si la texture n'est pas lisible CPU
        if (!tex.isReadable)
        {
            Debug.LogError("HeightMap '" + tex.name + "' non lisible : sélectionne la texture dans le Project, "
                         + "Inspector > coche 'Read/Write' > Apply.");
            return false;
        }

        Vector2[] uv = p_mesh.uv;

        for (int i = 0; i < p_vertices.Length; i++)
        {
            // GetPixelBilinear prend u,v normalisés [0,1] : gère automatiquement
            // les textures non carrées et de résolution différente de la grille
            Color couleur = tex.GetPixelBilinear(uv[i].x, uv[i].y);
            float niveauGris = couleur.grayscale;

            p_vertices[i] = new Vector3(p_vertices[i].x, niveauGris * amplitudeTexture, p_vertices[i].z);
        }

        miseAJourMeshApresDeformation();
        return true;
    }


    // ---- Point 7 (picking) : ajoute une colline gaussienne à l'endroit cliqué ----
    private void appliquerCollineAuPointeur()
    {
        if (p_cam == null) p_cam = Camera.main;
        if (p_cam == null || Mouse.current == null) return;

        Ray rayon = p_cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (!Physics.Raycast(rayon, out RaycastHit impact, 5000f, maskPickingTerrain)) return;

        // point d'impact converti dans l'espace local du terrain (le terrain peut être tourné/déplacé)
        Vector3 pointLocal = transform.InverseTransformPoint(impact.point);

        for (int i = 0; i < p_vertices.Length; i++)
        {
            float dx = p_vertices[i].x - pointLocal.x;
            float dz = p_vertices[i].z - pointLocal.z;
            float d2 = dx * dx + dz * dz;
            float delta = hauteurColline * Mathf.Exp(-d2 / (2f * rayonColline * rayonColline));
            p_vertices[i].y += delta; // additif : les collines successives s'accumulent
        }

        miseAJourMeshApresDeformation();
    }


    // ---- Point 7 : randomise les paramètres de la fonction en cours pour varier les résultats ----
    private void randomiserParametresFonction()
    {
        switch (typeFonction)
        {
            case TypeFonction.Sinusoide:
                amplitudeSinus = Random.Range(2f, 10f);
                frequenceSinus = Random.Range(0.05f, 0.2f);
                break;
            case TypeFonction.Collines:
                hauteurColline = Random.Range(10f, 30f);
                rayonColline = Random.Range(10f, 30f);
                break;
            case TypeFonction.Perlin:
                amplitudePerlin = Random.Range(5f, 25f);
                echellePerlin = Random.Range(0.02f, 0.1f);
                break;
        }
    }


    private void miseAJourMeshApresDeformation()
    {
        for (uint i = 0; i < p_vertices.Length; i++)
            calculerNormaleVertex(i);

        p_mesh.vertices = p_vertices;
        p_mesh.normals = p_normals;

        // forcer le MeshCollider à reprendre en compte la nouvelle géométrie
        p_meshCollider.sharedMesh = null;
        p_meshCollider.sharedMesh = p_mesh;
    }


    // ---- Point 12 : recalcule uniquement les normales (sans toucher à la hauteur) ----
    private void recalculerNormales()
    {
        for (uint i = 0; i < p_vertices.Length; i++)
            calculerNormaleVertex(i);

        p_mesh.normals = p_normals;
    }


    // ---- Point 11 : remaillage plan initial (hauteur 0 partout) ----
    private void reinitialiserMeshPlat()
    {
        for (int i = 0; i < p_vertices.Length; i++)
            p_vertices[i] = new Vector3(p_vertices[i].x, 0f, p_vertices[i].z);

        for (int i = 0; i < p_normals.Length; i++)
            p_normals[i] = Vector3.up;

        p_mesh.vertices = p_vertices;
        p_mesh.normals = p_normals;

        p_meshCollider.sharedMesh = null;
        p_meshCollider.sharedMesh = p_mesh;
    }


    private Vector3 normaleTriangle(int indexPremierSommet)
    {
        int ia = p_triangles[indexPremierSommet];
        int ib = p_triangles[indexPremierSommet + 1];
        int ic = p_triangles[indexPremierSommet + 2];

        Vector3 edge1 = p_vertices[ib] - p_vertices[ia];
        Vector3 edge2 = p_vertices[ic] - p_vertices[ia];

        return Vector3.Cross(edge1, edge2); // NON normalisé : sa norme = 2 x aire du triangle
    }

    public enum StrategieNormale { Basique, PondereeParSurface, PondereeParAngle }
    public StrategieNormale strategieNormale = StrategieNormale.Basique;
    private List<int>[] p_trianglesParVertex;

    private void calculerNormaleVertex(uint num_Vertex)
    {
        List<int> triangles = p_trianglesParVertex[num_Vertex]; // voir baking ci-dessous

        Vector3 somme = Vector3.zero;
        float poidsTotal = 0f;

        foreach (int t in triangles)
        {
            Vector3 brute = normaleTriangle(t);
            float poids;

            switch (strategieNormale)
            {
                case StrategieNormale.Basique:
                    poids = 1f;                       // a) moyenne simple
                    break;

                case StrategieNormale.PondereeParSurface:
                    poids = brute.magnitude;          // b) proportionnel à l'aire (‖cross‖ = 2×aire)
                    break;

                default: // PondereeParAngle
                    poids = angleAuSommet(t, (int)num_Vertex); // c) proportionnel à l'angle au sommet
                    break;
            }

            somme += brute.normalized * poids;
            poidsTotal += poids;
        }

        p_normals[num_Vertex] = (poidsTotal > 0f ? somme / poidsTotal : Vector3.up).normalized;
    }



    private float angleAuSommet(int indexPremierSommet, int indexVertex)
    {
        int ia = p_triangles[indexPremierSommet];
        int ib = p_triangles[indexPremierSommet + 1];
        int ic = p_triangles[indexPremierSommet + 2];

        Vector3 sommet, autre1, autre2;
        if (indexVertex == ia) { sommet = p_vertices[ia]; autre1 = p_vertices[ib]; autre2 = p_vertices[ic]; }
        else if (indexVertex == ib) { sommet = p_vertices[ib]; autre1 = p_vertices[ia]; autre2 = p_vertices[ic]; }
        else { sommet = p_vertices[ic]; autre1 = p_vertices[ia]; autre2 = p_vertices[ib]; }

        Vector3 v1 = (autre1 - sommet).normalized;
        Vector3 v2 = (autre2 - sommet).normalized;

        return Mathf.Acos(Mathf.Clamp(Vector3.Dot(v1, v2), -1f, 1f)); // angle en radians
    }


    private void calculerVoisinageTriangles()
    {
        p_trianglesParVertex = new List<int>[p_dimVertices];
        for (int i = 0; i < p_dimVertices; i++)
            p_trianglesParVertex[i] = new List<int>();

        // on parcourt p_triangles par pas de 3 (un triangle = 3 indices)
        for (int t = 0; t < p_triangles.Length; t += 3)
        {
            p_trianglesParVertex[p_triangles[t]].Add(t);
            p_trianglesParVertex[p_triangles[t + 1]].Add(t);
            p_trianglesParVertex[p_triangles[t + 2]].Add(t);
        }
    }

    private void creerLeMeshTerrain()
    {
        // 1. résolution réelle = 2^n
        resolution = (ushort)Mathf.Pow(2, puissance2Resolution);

        // 2. nb de sommets / triangles
        p_dimVertices = (uint)(resolution * resolution);
        p_dimTriangles = (uint)(2 * (resolution - 1) * (resolution - 1));

        // 3. écart entre deux sommets voisins
        p_dimInterVertices = dimension / (resolution - 1);

        // 4. allocation
        p_vertices = new Vector3[p_dimVertices];
        p_normals = new Vector3[p_dimVertices];
        Vector2[] uv = new Vector2[p_dimVertices];
        p_triangles = new int[p_dimTriangles * 3];

        float offset = CentrerPivot ? -dimension / 2f : 0f;

        // 5. génération des sommets, UV, normales provisoires
        for (int i = 0; i < resolution; i++)       // le long de Z
        {
            for (int j = 0; j < resolution; j++)   // le long de X
            {
                int index = i * resolution + j;

                float x = j * p_dimInterVertices + offset;
                float z = i * p_dimInterVertices + offset;

                p_vertices[index] = new Vector3(x, 0f, z);
                p_normals[index] = Vector3.up; // plan plat : normale verticale
                uv[index] = new Vector2(
                    (float)j / (resolution - 1),
                    (float)i / (resolution - 1));
            }
        }

        // 6. génération des triangles (2 par carreau)
        int ti = 0;
        for (int i = 0; i < resolution - 1; i++)
        {
            for (int j = 0; j < resolution - 1; j++)
            {
                int a = i * resolution + j;         // bas-gauche
                int b = (i + 1) * resolution + j;   // haut-gauche
                int c = i * resolution + j + 1;     // bas-droite
                int d = (i + 1) * resolution + j + 1;// haut-droite

                p_triangles[ti++] = a;
                p_triangles[ti++] = b;
                p_triangles[ti++] = c;

                p_triangles[ti++] = c;
                p_triangles[ti++] = b;
                p_triangles[ti++] = d;
            }
        }

        // 7. construction effective du Mesh
        p_mesh = new Mesh();
        p_mesh.name = "TerrainMesh";

        // IMPORTANT : au-delà de 65 535 sommets, l'index par défaut (16 bits) déborde
        if (p_dimVertices > 65535)
            p_mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        p_mesh.vertices = p_vertices;
        p_mesh.uv = uv;
        p_mesh.triangles = p_triangles;
        p_mesh.normals = p_normals;

        calculerVoisinageTriangles(); // baking, une seule fois

        p_meshFilter.mesh = p_mesh;
        p_meshCollider.sharedMesh = p_mesh;

    }

    void Start()
    {
        creerLeMeshTerrain();


    }

    // Update is called once per frame
    void Update()
    {
        p_fps = Mathf.Lerp(p_fps, 1f / Mathf.Max(Time.unscaledDeltaTime, 0.0001f), 0.1f);

        // ---- Point 7 : F2 = fonction suivante ----
        if (Keyboard.current.f2Key.wasPressedThisFrame)
        {
            choixModeDeformation = ChoixModeDeformation.Fonction;
            randomiserParametresFonction();
            appliquerDeformation_Fonction();
            // la fonction suivante sera utilisée au prochain appel     
            typeFonction = (TypeFonction)(((int)typeFonction + 1) % 3);
        }

        // ---- Point 7 (picking) : clic gauche en mode Collines = ajoute une colline au pointeur ----
        if (choixModeDeformation == ChoixModeDeformation.Fonction
            && typeFonction == TypeFonction.Collines
            && Mouse.current != null
            && Mouse.current.leftButton.wasPressedThisFrame)
        {
            appliquerCollineAuPointeur();
        }

        // ---- Point 8 : F3 = heightmap suivante ----
        if (Keyboard.current.f3Key.wasPressedThisFrame)
        {
            if (appliquerDeformation_Texture())
            {
                choixModeDeformation = ChoixModeDeformation.Texture;
                // l'image suivante sera utilisée au prochain appel
                numTexture = (numTexture + 1) % textures.Count;
            }
        }

        // ---- Point 10 : F10 maintenu 3s = mode d'affichage des normales suivant ----
        if (Keyboard.current.f10Key.isPressed)
        {
            p_dureeAppuiF10 += Time.deltaTime;
            if (p_dureeAppuiF10 >= delaiAppuiLongF10 && !p_f10DejaDeclenche)
            {
                p_f10DejaDeclenche = true;
                int nbModes = System.Enum.GetValues(typeof(ModeAffichageNormales)).Length;
                modeAffichageNormales = (ModeAffichageNormales)(((int)modeAffichageNormales + 1) % nbModes);
                Debug.Log("F10 : affichage normales = " + modeAffichageNormales);
            }
        }
        else
        {
            p_dureeAppuiF10 = 0f;
            p_f10DejaDeclenche = false;
        }

        // ---- Point 11 : F11 x2 consécutifs = remaillage plan ----
        if (Keyboard.current.f11Key.wasPressedThisFrame)
        {
            if (Time.time - p_dernierAppuiF11 <= delaiDoubleAppuiF11)
            {
                reinitialiserMeshPlat();
                Debug.Log("F11 x2 : maillage remis à plat");
                p_dernierAppuiF11 = -10f; // évite qu'un 3e appui compte comme un nouveau double-appui
            }
            else
            {
                p_dernierAppuiF11 = Time.time;
            }
        }

        // ---- Point 12 : F12 = mode de calcul des normales suivant ----
        if (Keyboard.current.f12Key.wasPressedThisFrame)
        {
            strategieNormale = (StrategieNormale)(((int)strategieNormale + 1) % 3);
            recalculerNormales();
            Debug.Log("F12 : calcul des normales = " + strategieNormale);
        }

        // ---- Point 13 : F1 = afficher/masquer l'aide ----
        if (Keyboard.current.f1Key.wasPressedThisFrame)
        {
            p_afficherAide = !p_afficherAide;
        }
    }


    // ==================== Point 10 : rendu des normales via GL ====================

    private void creerMaterialLignes()
    {
        Shader shader = Shader.Find("Hidden/Internal-Colored");
        if (shader == null) return;

        p_materialLignes = new Material(shader);
        p_materialLignes.hideFlags = HideFlags.HideAndDontSave;
        p_materialLignes.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        p_materialLignes.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        p_materialLignes.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        p_materialLignes.SetInt("_ZWrite", 0);
    }

    // URP/HDRP : OnRenderObject n'est JAMAIS appelé, il faut s'abonner à ce callback
    void OnEnable()
    {
        UnityEngine.Rendering.RenderPipelineManager.endCameraRendering += surFinRenduCamera;
    }

    void OnDisable()
    {
        UnityEngine.Rendering.RenderPipelineManager.endCameraRendering -= surFinRenduCamera;
    }

    private void surFinRenduCamera(UnityEngine.Rendering.ScriptableRenderContext contexte, Camera camera)
    {
        // uniquement la caméra du jeu (pas les previews)
        if (camera.cameraType == CameraType.Game || camera.cameraType == CameraType.SceneView)
            dessinerNormales();
    }

    // pipeline Built-in uniquement
    void OnRenderObject()
    {
        if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline == null)
            dessinerNormales();
    }

    private void dessinerNormales()
    {
        if (modeAffichageNormales == ModeAffichageNormales.Aucune || p_materialLignes == null || p_mesh == null)
            return;

        p_materialLignes.SetPass(0);
        GL.PushMatrix();
        GL.MultMatrix(transform.localToWorldMatrix);
        GL.Begin(GL.LINES);

        bool afficherVertices = modeAffichageNormales == ModeAffichageNormales.NormalesVertices
                              || modeAffichageNormales == ModeAffichageNormales.OrientationEtEclairage;
        bool afficherEclairage = modeAffichageNormales == ModeAffichageNormales.NormalesEclairage
                               || modeAffichageNormales == ModeAffichageNormales.OrientationEtEclairage;
        bool afficherOrientation = modeAffichageNormales == ModeAffichageNormales.NormaleOrientation
                                 || modeAffichageNormales == ModeAffichageNormales.OrientationEtEclairage;

        // a) normales aux vertices (une par sommet)
        if (afficherVertices)
        {
            GL.Color(Color.green);
            for (int i = 0; i < p_vertices.Length; i++)
                dessinerLigne(p_vertices[i], p_normals[i]);
        }

        // b) normale d'éclairage = moyenne des 3 normales de sommet du triangle, tracée au centre du triangle
        if (afficherEclairage)
        {
            GL.Color(Color.cyan);
            for (int t = 0; t < p_triangles.Length; t += 3)
            {
                Vector3 centre = (p_vertices[p_triangles[t]] + p_vertices[p_triangles[t + 1]] + p_vertices[p_triangles[t + 2]]) / 3f;
                Vector3 normaleMoyenne = (p_normals[p_triangles[t]] + p_normals[p_triangles[t + 1]] + p_normals[p_triangles[t + 2]]).normalized;
                dessinerLigne(centre, normaleMoyenne);
            }
        }

        // c) normale d'orientation = produit vectoriel V01 ^ V02 (normale géométrique brute du triangle)
        if (afficherOrientation)
        {
            GL.Color(Color.red);
            for (int t = 0; t < p_triangles.Length; t += 3)
            {
                Vector3 centre = (p_vertices[p_triangles[t]] + p_vertices[p_triangles[t + 1]] + p_vertices[p_triangles[t + 2]]) / 3f;
                Vector3 normaleFace = normaleTriangle(t).normalized;
                dessinerLigne(centre, normaleFace);
            }
        }

        GL.End();
        GL.PopMatrix();
    }

    private void dessinerLigne(Vector3 origineLocale, Vector3 directionLocale)
    {
        GL.Vertex(origineLocale);
        GL.Vertex(origineLocale + directionLocale * longueurAffichageNormales);
    }


    // ==================== Point 13 : fenêtre d'aide (F1) ====================

    void OnGUI()
    {
        if (!p_afficherAide) return;

        float largeur = 440f;
        float hauteur = 400f;
        Rect zone = new Rect((Screen.width - largeur) / 2f, (Screen.height - hauteur) / 2f, largeur, hauteur);

        GUI.Box(zone, "");
        GUILayout.BeginArea(zone);

        GUILayout.Label("<b>Aide / Informations (F1)</b>");
        GUILayout.Space(8);

        long memoireOctets = (p_vertices.Length * 3 * 4L) * 2 + p_triangles.Length * 4L; // vertices + normales + indices
        GUILayout.Label("<b>Maillage</b>");
        GUILayout.Label($"Vertices : {p_dimVertices}");
        GUILayout.Label($"Triangles : {p_dimTriangles}");
        GUILayout.Label($"Mémoire approx. : {memoireOctets / 1024f:F1} Ko");
        GUILayout.Space(8);

        GUILayout.Label("<b>Paramètres actifs</b>");
        GUILayout.Label($"Mode calcul normales : {strategieNormale}");
        GUILayout.Label($"Mode affichage normales : {modeAffichageNormales}");
        GUILayout.Label($"FPS : {p_fps:F0}");
        GUILayout.Space(8);

        GUILayout.Label("<b>Contrôles</b>");
        GUILayout.Label("F1 : afficher/masquer cette aide");
        GUILayout.Label("F2 : fonction suivante (sinus / colline / perlin)");
        GUILayout.Label("Clic gauche (mode Colline) : ajoute une colline au pointeur");
        GUILayout.Label("F3 : heightmap suivante");
        GUILayout.Label("F10 (maintenir 3s) : mode d'affichage des normales suivant");
        GUILayout.Label("F11 x2 : réinitialise le maillage à plat");
        GUILayout.Label("F12 : mode de calcul des normales suivant");
        GUILayout.Label("ZQSD / WASD + clic droit souris : déplacer la caméra");
        GUILayout.Label("Flèches gauche/droite : faire tourner le terrain");

        GUILayout.EndArea();
    }
}