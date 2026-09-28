using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using UnityEngine;

public class CreateCubeMultiRes : MonoBehaviour
{

    private Mesh p_mesh;
    private Vector3[] p_vertices;
    private int[] p_triangles;
    private Vector3[] p_normals;
    public float width = 1.0f;
    Vector3 p0, p1, p2, p3, p4, p5, p6, p7;

    public int res = 4;          // résolution : nb de subdivisions par face
    public float widthCube = 1.0f; // taille du cube (utilisée par construireFace)

    private int nb_vertices_par_face;
    private int nb_vertices;
    private int nb_triangles_par_face;
    private int nb_triangles;
    private int indexTriangle;

    private Camera cam;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        CreerCubeMultiRes();
        cam = Camera.main;
    }

    // Update is called once per frame
    void Update()
    {
        
        if (Input.GetMouseButtonDown(0))
        {
            // va permettre de filtrer les object qui peut etre intercepté par le rayon de picking
            LayerMask maskPickingObjects = LayerMask.GetMask("L_PickingObject");
            // remarque
            // la variable cam a été déclaré private dans la classe et initialisée dans Start : cam = Camera.main;

            // il faut que la caméra par defaut ait le tag MainCamera pour que le système la reconnaisse comme Camera.main
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            Debug.DrawRay(ray.origin, ray.direction * 100, Color.magenta, 5);
            // lancer un rayon et trouver les objets de la bonne layer qui entre en collision avec le
            // rayon ; l’argument HIT contient en sortie toutes les informations utiles
            RaycastHit hit;
            if (!Physics.Raycast(ray, out hit, Mathf.Infinity, maskPickingObjects))
                return;

            // on a sélectionné un triangle du cube avec le rayon (possible si MeshCollider !
            // récupérer le numéro du triangle touché
            int numSelectedTri = hit.triangleIndex;

            Debug.Log(numSelectedTri);


            // récupérer les indices des 3 sommets du triangle touché

            // récupérer la normale du triangle touché
            Vector3 normSelTri = hit.transform.InverseTransformDirection(hit.normal).normalized;


            // Calcul du barycentre , position central du triangle sélectionné par picking
            Vector3 barySelectedTri = Vector3.zero;
            for (int i = 0; i < 3; i++)
                barySelectedTri += p_vertices[p_triangles[numSelectedTri * 3 + i]];
            // recherche et modification des vertices proche du triangle sélectionné, distance inférieur à un seuil voisinageDef(parametre public de la classe modifiable depuis l’IDE)
            float decalage = 2f;
            float voisinageDef = 5f;
            for (int i = 0; i < p_vertices.Length; i++)
            {
                float dist = Vector3.Distance(p_vertices[i], barySelectedTri);
                Debug.Log(dist);
                Debug.Log(voisinageDef);
                Debug.Log("  ");
                if (dist < voisinageDef)
                {
                    // facteur d'atténuation : 1 au centre (barycentre), 0 en bordure du voisinage
                    // (déformation plus forte au centre, plus douce sur les bords => effet "bosse")
                    float facteur = 1f - (dist / voisinageDef);
                    Debug.Log("modif");

                    p_vertices[i] += normSelTri * decalage * facteur;
                }
            }
            // on réassigne le tableau de vertices modifié au mesh (obligatoire, Unity ne travaille que sur une copie)
            p_mesh.vertices = p_vertices;
            p_mesh.triangles = p_triangles;
            // mettre à jour le maillage de l’objet et son collider et ses bounds
            p_mesh.RecalculateNormals();
            p_mesh.RecalculateBounds();

            GetComponent<MeshFilter>().mesh = p_mesh;
            GetComponent<MeshCollider>().sharedMesh = null;
            GetComponent<MeshCollider>().sharedMesh = p_mesh;

            DebugNormals(true, true, true);

        }
    }

    private Vector3 normaleDuTriangle(int num_triangle)
    {
        int idx = num_triangle * 3; // num_triangle est un index de triangle, on retrouve l'index dans p_triangles
        Vector3 v1 = p_vertices[p_triangles[idx + 1]] - p_vertices[p_triangles[idx]];
        Vector3 v2 = p_vertices[p_triangles[idx + 2]] - p_vertices[p_triangles[idx]];
        return Vector3.Cross(v1, v2).normalized;
    }

    private void CreerCubeMultiRes()
    {
        // res est une donnée membre publique, paramétrable dans l'IDE
        long nb_vertices_par_face_théoriques = (res + 1) * (res + 1);
        long nb_vertices_théoriques = nb_vertices_par_face_théoriques * 6;
        if (nb_vertices_théoriques >= ushort.MaxValue)
        {
            print("trop de vertices pour type d'indices ushort par defaut ");
            print("max possible " + ushort.MaxValue + " demandé = " +
            nb_vertices_théoriques);
            print("il faudrait modifier le type des indices avec mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; ");
            return;
        }
        nb_triangles_par_face = (ushort)(2 * res * res);
        nb_vertices_par_face = (ushort)nb_vertices_par_face_théoriques;
        nb_vertices = (ushort)nb_vertices_théoriques;
        nb_triangles = (ushort)(nb_triangles_par_face * 6);
        p_mesh = new Mesh();
        p_mesh.name = "MyProceduralCubeMultiRes";
        p_vertices = new Vector3[nb_vertices];
        p_normals = new Vector3[nb_vertices];
        p_triangles = new int[nb_triangles * 3];

        // Création des vertices et des triangles de chaque face
        indexTriangle = 0;
        construireFace(0, Vector3.right, Vector3.up, Vector3.back); // face avant
        construireFace(1, Vector3.left, Vector3.up, Vector3.forward); // face arrière
        construireFace(2, Vector3.forward, Vector3.up, Vector3.right); // face droite
        construireFace(3, Vector3.back, Vector3.up, Vector3.left); // face gauche
        construireFace(4, Vector3.right, Vector3.forward, Vector3.up); // face dessus
        construireFace(5, Vector3.left, Vector3.forward, Vector3.down); // face dessous
                                                                        // CALCUL des NORMALES
        for (int num_face = 0; num_face < 6; num_face++)
        {
            // on la calcule pour le premier triangle de la face
            Vector3 normalFaceEnCours = normaleDuTriangle(nb_triangles_par_face * num_face);

            // on la recopie pour tous les autres triangles de la même face
            for (int i = 0; i <= res; i++) //
                for (int j = 0; j <= res; j++)
                    p_normals[num_face * nb_vertices_par_face + i * (res + 1) + j] =
                    normalFaceEnCours;
        }
        p_mesh.Clear();
        p_mesh.vertices = p_vertices;
        p_mesh.triangles = p_triangles;
        p_mesh.normals = p_normals;
        GetComponent<MeshFilter>().mesh = p_mesh;

        GetComponent<MeshCollider>().sharedMesh = null;
        GetComponent<MeshCollider>().sharedMesh = p_mesh;

    }


    private void construireFace(int numero_face, Vector3 axeDroit, Vector3 axeHaut, Vector3 axeProfondeur)
    {
        // construire VERTICES
        float decal = widthCube / 2f;
        for (int i = 0; i <= res; i++)
            for (int j = 0; j <= res; j++)
                p_vertices[numero_face * nb_vertices_par_face + i * (res + 1) + j] =
                axeHaut * (-decal + (float)(i) / res * widthCube) +
                axeDroit * (-decal + (float)(j) / res * widthCube) +
                axeProfondeur * decal;
        // construire TRIANGLES
        int num_vertex;
        for (int i = 0; i < res; i++)
            for (int j = 0; j < res; j++)
            {
                num_vertex = numero_face * nb_vertices_par_face + i * (res + 1) + j;
                p_triangles[indexTriangle++] = num_vertex;
                p_triangles[indexTriangle++] = num_vertex + res + 1;
                p_triangles[indexTriangle++] = num_vertex + 1;
                p_triangles[indexTriangle++] = num_vertex + res + 1;
                p_triangles[indexTriangle++] = num_vertex + res + 1 + 1;
                p_triangles[indexTriangle++] = num_vertex + 1;
            }
    }

    private void DebugNormals(bool affN_Orientation, bool affN_Eclairage, bool affN_vertices)
    {

        //if (affN_Orientation)
        //{
        //    Vector3 vposmoyenne, v1, v2, vnormetri;
        //    for (int num_tri = 0; num_tri < p_triangles.Length; num_tri += 3)
        //    { // calcul de vnormetri comme le produit vectoriel de V01^V02
        //        vposmoyenne = p_vertices[p_triangles[num_tri]] + p_vertices[p_triangles[num_tri + 1]] +
        //        p_vertices[p_triangles[num_tri + 2]];
        //        vposmoyenne /= 3.0f;
        //        v1 = p_vertices[p_triangles[num_tri + 1]] - p_vertices[p_triangles[num_tri + 0]];
        //        v2 = p_vertices[p_triangles[num_tri + 2]] - p_vertices[p_triangles[num_tri + 0]];
        //        vnormetri = Vector3.Cross(v1, v2);
        //        Debug.DrawRay(transform.position + vposmoyenne, vnormetri.normalized * 3.0f, Color.yellow, 5,
        //       false);
        //    }
        //}
        //if (affN_Eclairage)
        //{
        //    Vector3 vposmoyenne, vnormmoyenne;
        //    for (int num_tri = 0; num_tri < p_triangles.Length; num_tri += 3)
        //    {
        //        vposmoyenne = (p_vertices[p_triangles[num_tri]] + p_vertices[p_triangles[num_tri + 1]] +
        //        p_vertices[p_triangles[num_tri + 2]]) / 3.0f;
        //        vnormmoyenne = (p_normals[p_triangles[num_tri]] + p_normals[p_triangles[num_tri + 1]] +
        //        p_normals[p_triangles[num_tri + 2]]) / 3.0f;
        //        Debug.DrawRay(transform.position + vposmoyenne, vnormmoyenne.normalized * 2.0f, Color.green, 5, false);
        //    }
        //}
        if (affN_vertices)
        {
            for (int num_vert = 0; num_vert < p_vertices.Length; num_vert++)
                Debug.DrawRay(transform.position + p_vertices[num_vert], p_normals[num_vert].normalized * 1.0f, Color.red,
               5, false);
        }
    }

}
