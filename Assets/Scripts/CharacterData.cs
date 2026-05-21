using UnityEngine;

/// <summary>
/// 角色数据 — 每个角色的属性、技能、素材路径
/// </summary>
public class CharacterData
{
    public string id;
    public string displayName;
    public string typeName;
    public string description;
    public string skillName;
    public string skillDesc;
    public SkillType skillType;
    public Color characterColor;
    public int speed;
    public int power;
    public int technique;

    // 素材路径
    public string poseBasePath;
    public string posePrefix;

    // 预解析的pose路径
    public string idlePath;
    public string[] runPaths;
    public string kickPath;
    public string attackKickPath;
    public string jumpPath;
    public string hurtPath;
    public string hitPath;
    public string slidePath;
    public string duckPath;
    public string fallPath;
    public string fallDownPath;

    public string previewPath => idlePath;
}

/// <summary>
/// 角色数据库 — 硬编码4个角色，每个角色分配不同的技能
/// </summary>
public static class CharacterDatabase
{
    private static readonly string BasePath = "D:/cc/暴走足球/人物素材包/";
    private static CharacterData[] characters;
    public static int Count => 4;

    public static CharacterData Get(int index)
    {
        if (characters == null) Init();
        index = Mathf.Clamp(index, 0, characters.Length - 1);
        return characters[index];
    }

    public static CharacterData[] GetAll()
    {
        if (characters == null) Init();
        return characters;
    }

    static void Init()
    {
        characters = new CharacterData[4];

        // 角色0: 男性冒险者 — 强力射门（PowerShot）
        characters[0] = Create(
            "maleAdventurer", "Male adventurer", "character_maleAdventurer_",
            "Power Shot", "Power",
            "Powerful kick, doubles shot power",
            "Power Shot", "Next kick force x2, lasts 5s",
            SkillType.PowerShot, Color.yellow, 5, 2, 3
        );

        // 角色1: 机器人 — 加速冲刺（SpeedBoost）
        characters[1] = Create(
            "robot", "Robot", "character_robot_",
            "Speed Boost", "Speed",
            "Incredible speed burst",
            "Speed Boost", "Move speed x2 for 3 seconds",
            SkillType.SpeedBoost, Color.red, 2, 5, 3
        );

        // 角色2: 女性 — 护盾（Shield）
        characters[2] = Create(
            "femalePerson", "Female person", "character_femalePerson_",
            "Shield", "Defense",
            "Blocks one powerful shot",
            "Shield", "Block one incoming shot, lasts 5s",
            SkillType.Shield, Color.green, 3, 3, 5
        );

        // 角色3: 僵尸 — 分裂球（SplitBall）
        characters[3] = Create(
            "zombie", "Zombie", "character_zombie_",
            "Phantom", "Technique",
            "Stealth moves, surprise attacks",
            "Split Ball", "Next kick splits into 3 balls",
            SkillType.SplitBall, new Color(0.5f, 0, 1f), 4, 3, 4
        );
    }

    static CharacterData Create(
        string id, string folder, string prefix,
        string displayName, string typeName, string desc,
        string skillName, string skillDesc, SkillType skillType,
        Color color, int spd, int pow, int tec)
    {
        string basePath = BasePath + folder + "/PNG/Poses/";

        var d = new CharacterData();
        d.id = id;
        d.displayName = displayName;
        d.typeName = typeName;
        d.description = desc;
        d.skillName = skillName;
        d.skillDesc = skillDesc;
        d.skillType = skillType;
        d.characterColor = color;
        d.speed = spd;
        d.power = pow;
        d.technique = tec;
        d.poseBasePath = basePath;
        d.posePrefix = prefix;

        d.idlePath = basePath + prefix + "idle.png";
        d.runPaths = new string[] {
            basePath + prefix + "run0.png",
            basePath + prefix + "run1.png",
            basePath + prefix + "run2.png"
        };
        d.kickPath = basePath + prefix + "kick.png";
        d.attackKickPath = basePath + prefix + "attackKick.png";
        d.jumpPath = basePath + prefix + "jump.png";
        d.hurtPath = basePath + prefix + "hurt.png";
        d.hitPath = basePath + prefix + "hit.png";
        d.slidePath = basePath + prefix + "slide.png";
        d.duckPath = basePath + prefix + "duck.png";
        d.fallPath = basePath + prefix + "fall.png";
        d.fallDownPath = basePath + prefix + "fallDown.png";

        return d;
    }
}
