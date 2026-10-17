using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Dropy.Gameplay
{
    /// <summary>
    /// "Bina İncelemesi" kartının içi: vakanın binası iki render texture'a çizilir (dış görünüş ve iskelet).
    /// Sürükleyince bina döner; fare kartın üstündeyken imleç büyütece dönüşür ve dairenin içinde iskelet görünür.
    /// Kusurlar iskelette kırmızıdır; büyüteç bir kusurun üstündeyken tıklamak onu müteahhide sorar
    /// ve kusur maket üzerinde de işaretli kalır.
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    public class BuildingViewer : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler,
        IPointerClickHandler, IPointerMoveHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        const float LensRadius = 74f;
        const float Fov = 30f;
        const int Supersample = 2;
        static readonly Color ModelBackground = new Color(0.83f, 0.86f, 0.84f);
        static readonly Color WireBackground = new Color(0.08f, 0.035f, 0.04f);
        static readonly Color Handle = new Color(0.2f, 0.13f, 0.08f);

        RawImage image;
        RectTransform rect;
        RectTransform card;
        DeskTools tools;
        BuildingModel model;
        RenderTexture modelTexture;
        RenderTexture wireTexture;
        Material lensMaterial;
        Material solidMaterial;
        Material lineMaterial;
        Material fillMaterial;
        MaterialPropertyBlock block;
        CommandBuffer commands;
        RectTransform handle;
        RectTransform label;
        TextMeshProUGUI labelText;
        CanvasGroup visibility;

        float yaw = -32f;
        float pitch = 16f;
        bool hovering;
        bool dragging;
        Vector2 pointer;
        Vector2 lensUv = new Vector2(0.5f, 0.5f);
        ModelDefect hovered;
        bool cursorHidden;

        /// <summary>Büyüteç bir kusurdayken tıklandı: o kusur müteahhide soruluyor.</summary>
        public event System.Action<DocumentField> DefectAsked;

        public static BuildingViewer Create(RectTransform parent, RectTransform card, BuildingCase buildingCase, DeskTools tools)
        {
            var rt = Ui.Rect("BuildingView", parent);
            var raw = rt.gameObject.AddComponent<RawImage>();
            var viewer = rt.gameObject.AddComponent<BuildingViewer>();
            viewer.image = raw;
            viewer.rect = rt;
            viewer.card = card;
            viewer.tools = tools;
            viewer.model = BuildingModel.Build(buildingCase);
            return viewer;
        }

        void Start()
        {
            visibility = GetComponentInParent<CanvasGroup>();
            block = new MaterialPropertyBlock();
            commands = new CommandBuffer { name = "Bina incelemesi" };

            var modelShader = Shader.Find("Dropy/InspectModel");
            solidMaterial = new Material(modelShader);
            lineMaterial = new Material(modelShader);
            lineMaterial.SetFloat("_Shade", 0.35f);
            fillMaterial = new Material(modelShader);
            fillMaterial.SetFloat("_Shade", 0f);
            fillMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            fillMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            fillMaterial.SetFloat("_ZWrite", 0f);

            lensMaterial = new Material(Shader.Find("Dropy/InspectLens"));
            image.material = lensMaterial;

            // Büyüteç sapı: dairenin sağ altından dışarı uzanan koyu tahta.
            handle = Ui.Image(rect, "LensHandle", Handle, Ui.Rounded, raycast: false).rectTransform;
            handle.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(20f, 86f));
            handle.localRotation = Quaternion.Euler(0f, 0f, 45f);
            var collar = Ui.Image(handle, "Collar", new Color(0.72f, 0.6f, 0.36f), Ui.Rounded, raycast: false).rectTransform;
            collar.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 2f), new Vector2(24f, 16f));

            label = Ui.Image(rect, "DefectLabel", new Color(0.1f, 0.04f, 0.04f, 0.92f), Ui.Rounded, raycast: false).rectTransform;
            label.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(300f, 64f));
            labelText = Ui.Text(label, "Text", "", 15f, Color.white, TextAlignmentOptions.Center);
            labelText.richText = true;
            labelText.rectTransform.Stretch(10f, 4f, 10f, 4f);

            SetLens(false);
        }

        void OnDestroy()
        {
            ShowCursor(true);
            model?.Destroy();
            if (modelTexture != null)
                modelTexture.Release();
            if (wireTexture != null)
                wireTexture.Release();
            Destroy(modelTexture);
            Destroy(wireTexture);
            Destroy(lensMaterial);
            Destroy(solidMaterial);
            Destroy(lineMaterial);
            Destroy(fillMaterial);
            commands?.Release();
        }

        void OnDisable() => ShowCursor(true);

        bool LensAllowed => hovering && (tools == null || tools.Active == Seal.None);

        void LateUpdate()
        {
            if (visibility != null && visibility.alpha <= 0.001f)
            {
                SetLens(false);
                return;
            }

            EnsureTextures();
            UpdateLens();
            Render();
        }

        void EnsureTextures()
        {
            var size = rect.rect.size;
            int w = Mathf.Max(64, Mathf.RoundToInt(size.x * Supersample));
            int h = Mathf.Max(64, Mathf.RoundToInt(size.y * Supersample));
            if (modelTexture != null && modelTexture.width == w && modelTexture.height == h)
                return;
            if (modelTexture != null)
            {
                modelTexture.Release();
                wireTexture.Release();
                Destroy(modelTexture);
                Destroy(wireTexture);
            }
            modelTexture = NewTexture(w, h, "BuildingModel");
            wireTexture = NewTexture(w, h, "BuildingWire");
            image.texture = modelTexture;
            lensMaterial.SetTexture("_WireTex", wireTexture);
        }

        static RenderTexture NewTexture(int w, int h, string name)
        {
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { name = name, antiAliasing = 4 };
            rt.Create();
            return rt;
        }

        // ---------------- Büyüteç ----------------

        void UpdateLens()
        {
            bool on = LensAllowed;
            if (on)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, pointer, null, out var local);
                var r = rect.rect;
                lensUv = new Vector2((local.x - r.xMin) / r.width, (local.y - r.yMin) / r.height);
                handle.anchoredPosition = local + new Vector2(1f, -1f).normalized * (LensRadius + 4f);
                // Etiket büyütecin karşı yarısında: üstteyken altına, alttayken üstüne (kart başlığını kapatmasın).
                bool upper = local.y > r.center.y;
                label.pivot = new Vector2(0.5f, upper ? 1f : 0f);
                label.anchoredPosition = local + new Vector2(upper ? -40f : 0f, upper ? -LensRadius - 14f : LensRadius + 14f);
                hovered = Pick(lensUv);
            }
            else
                hovered = null;

            SetLens(on);
            lensMaterial.SetVector("_LensCenter", lensUv);
            lensMaterial.SetVector("_ViewSize", rect.rect.size);
            lensMaterial.SetFloat("_LensRadius", LensRadius);
            lensMaterial.SetFloat("_LensOn", on ? 1f : 0f);

            if (hovered != null)
            {
                label.gameObject.SetActive(true);
                string hint = hovered.found ? "Müteahhide gösterildi" : "Tıkla: müteahhide göster";
                labelText.text = $"<b><color=#ff5a48>{hovered.title}</color></b>\n{hovered.detail}\n<size=80%><i>{hint}</i></size>";
            }
            else
                label.gameObject.SetActive(false);
        }

        void SetLens(bool on)
        {
            if (handle == null)
                return;
            handle.gameObject.SetActive(on);
            if (!on)
                label.gameObject.SetActive(false);
            ShowCursor(!on);
        }

        void ShowCursor(bool show)
        {
            if (show == !cursorHidden)
                return;
            cursorHidden = !show;
            Cursor.visible = show;
        }

        // ---------------- Çizim ----------------

        Matrix4x4 ModelMatrix => Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0f, yaw, 0f), Vector3.one);

        void ViewMatrices(out Matrix4x4 view, out Matrix4x4 projection)
        {
            var size = model.Size;
            float aspect = rect.rect.width / Mathf.Max(1f, rect.rect.height);
            var target = new Vector3(0f, size.y * 0.47f, 0f);
            // Binanın çevrel küresi hem dikey hem yatay görüş açısına sığsın.
            float radius = 0.5f * new Vector3(size.x + 2f, size.y + 1f, size.z + 2f).magnitude;
            float halfV = Fov * 0.5f * Mathf.Deg2Rad;
            float halfH = Mathf.Atan(Mathf.Tan(halfV) * aspect);
            float distance = radius / Mathf.Sin(Mathf.Min(halfV, halfH));
            var rotation = Quaternion.Euler(pitch, 0f, 0f);
            var position = target - rotation * Vector3.forward * distance;
            view = Matrix4x4.Scale(new Vector3(1f, 1f, -1f)) * Matrix4x4.TRS(position, rotation, Vector3.one).inverse;
            projection = Matrix4x4.Perspective(Fov, aspect, 0.3f, distance * 3f);
        }

        void Render()
        {
            if (modelTexture == null)
                return;
            ViewMatrices(out var view, out var projection);
            var world = ModelMatrix;
            float pulse = 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * 6f);

            commands.Clear();

            // Maket: dış görünüş, gösterilen kusurlar üstünde kırmızı.
            commands.SetRenderTarget(modelTexture);
            commands.ClearRenderTarget(true, true, ModelBackground);
            commands.SetViewProjectionMatrices(view, projection);
            commands.DrawMesh(model.Solid, world, solidMaterial);
            foreach (var d in model.Defects)
                if (d.found)
                    DrawFill(d, world, 0.55f + 0.25f * pulse);

            // İskelet: büyütecin içi.
            commands.SetRenderTarget(wireTexture);
            commands.ClearRenderTarget(true, true, WireBackground);
            commands.SetViewProjectionMatrices(view, projection);
            commands.DrawMesh(model.Wire, world, lineMaterial);
            foreach (var d in model.Defects)
            {
                bool hot = d == hovered;
                block.SetColor("_Color", hot ? Color.Lerp(Color.white, BuildingModel.DefectLine, 0.3f) : Color.white * (0.75f + 0.25f * pulse));
                commands.DrawMesh(d.bars, world, lineMaterial, 0, 0, block);
            }
            foreach (var d in model.Defects)
                DrawFill(d, world, d == hovered ? 2.2f : 0.7f + 0.5f * pulse);

            Graphics.ExecuteCommandBuffer(commands);
        }

        void DrawFill(ModelDefect d, Matrix4x4 world, float alpha)
        {
            block.SetColor("_Color", new Color(1f, 1f, 1f, alpha));
            commands.DrawMesh(d.fill, world, fillMaterial, 0, 0, block);
        }

        /// <summary>Büyütecin merkezinden binaya ışın: en küçük kusur kutusu kazanır (kolon yumuşak katın içindedir).</summary>
        ModelDefect Pick(Vector2 uv)
        {
            if (uv.x < 0f || uv.x > 1f || uv.y < 0f || uv.y > 1f)
                return null;
            ViewMatrices(out var view, out var projection);
            var inverse = (projection * view).inverse;
            var ndc = uv * 2f - Vector2.one;
            var near = inverse.MultiplyPoint(new Vector3(ndc.x, ndc.y, -1f));
            var far = inverse.MultiplyPoint(new Vector3(ndc.x, ndc.y, 1f));
            var toLocal = ModelMatrix.inverse;
            var origin = toLocal.MultiplyPoint(near);
            var direction = toLocal.MultiplyVector(far - near).normalized;
            var ray = new Ray(origin, direction);

            ModelDefect best = null;
            float bestVolume = float.MaxValue;
            foreach (var d in model.Defects)
            {
                if (!d.bounds.IntersectRay(ray))
                    continue;
                var s = d.bounds.size;
                float volume = s.x * s.y * s.z;
                if (volume < bestVolume)
                {
                    best = d;
                    bestVolume = volume;
                }
            }
            return best;
        }

        // ---------------- Girdi ----------------

        public void OnPointerEnter(PointerEventData eventData)
        {
            hovering = true;
            pointer = eventData.position;
        }

        public void OnPointerMove(PointerEventData eventData) => pointer = eventData.position;

        public void OnPointerExit(PointerEventData eventData)
        {
            hovering = false;
            if (!dragging)
                SetLens(false);
        }

        public void OnPointerDown(PointerEventData eventData) => card.SetAsLastSibling();

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.dragging || !LensAllowed || hovered == null)
                return;
            hovered.found = true;
            Sfx.Paper();
            DefectAsked?.Invoke(hovered.field);
        }

        public void OnBeginDrag(PointerEventData eventData) => dragging = true;

        public void OnDrag(PointerEventData eventData)
        {
            pointer = eventData.position;
            yaw -= eventData.delta.x * 0.35f;
            pitch = Mathf.Clamp(pitch - eventData.delta.y * 0.2f, -4f, 55f);
        }

        public void OnEndDrag(PointerEventData eventData) => dragging = false;
    }
}
