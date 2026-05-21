using UnityEngine;

public class BackgroundData
{
    public string id;
    public string displayName;
    public string[] layerPaths;
    public float[] layerAlphas;
    public float[] layerYOffsets;
    public string thumbnailPath;
}

public static class BackgroundDatabase
{
    private static readonly string BaseBgPath = "D:/cc/暴走足球/背景/";
    private static BackgroundData[] backgrounds;
    public static int Count => 12;

    public static BackgroundData Get(int index)
    {
        if (backgrounds == null) Init();
        index = Mathf.Clamp(index, 0, backgrounds.Length - 1);
        return backgrounds[index];
    }

    public static BackgroundData[] GetAll()
    {
        if (backgrounds == null) Init();
        return backgrounds;
    }

    static void Init()
    {
        backgrounds = new BackgroundData[12];

        // Pack A: background 1~4
        for (int i = 0; i < 4; i++)
        {
            string path = BaseBgPath + "background " + (i + 1) + "/";
            string[] names = { "Urban Alley", "Japanese Street", "Park Playground", "Indoor Court" };

            backgrounds[i] = new BackgroundData();
            backgrounds[i].id = "bg" + (i + 1);
            backgrounds[i].displayName = names[i];
            // Pack A: 4.png=farthest, 1.png=nearest
            backgrounds[i].layerPaths = new string[] {
                path + "4.png", path + "3.png", path + "2.png", path + "1.png"
            };
            backgrounds[i].layerAlphas = new float[] { 0.5f, 0.6f, 0.7f, 0.8f };
            backgrounds[i].layerYOffsets = new float[] { 2f, 1.5f, 1f, 0.5f };
            backgrounds[i].thumbnailPath = path + "orig.png";
        }

        // Pack B: Background_1~8
        string[] cityNames = {
            "Night City", "Day Town", "Sunset Road", "Neon Bar",
            "Hot District", "Foggy Street", "Downtown", "Red Night"
        };

        for (int i = 0; i < 8; i++)
        {
            string path = BaseBgPath + "City Backgrounds Pixel Art/PNG/Background_" + (i + 1) + "/";
            int layerCount = (i == 1 || i == 3) ? 6 : 5; // Background_2 and _4 have 6 layers

            string[] layers = new string[layerCount];
            for (int j = 0; j < layerCount; j++)
                layers[j] = path + "Layer_" + (j + 1) + ".png";

            float[] alphas = new float[layerCount];
            float[] yOffsets = new float[layerCount];
            for (int j = 0; j < layerCount; j++)
            {
                alphas[j] = Mathf.Lerp(0.4f, 0.9f, (float)j / (layerCount - 1));
                yOffsets[j] = Mathf.Lerp(3f, 0f, (float)j / (layerCount - 1));
            }

            backgrounds[4 + i] = new BackgroundData();
            backgrounds[4 + i].id = "city" + (i + 1);
            backgrounds[4 + i].displayName = cityNames[i];
            backgrounds[4 + i].layerPaths = layers;
            backgrounds[4 + i].layerAlphas = alphas;
            backgrounds[4 + i].layerYOffsets = yOffsets;
            backgrounds[4 + i].thumbnailPath = path + "Layer_1.png";
        }
    }
}
