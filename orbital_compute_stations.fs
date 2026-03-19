FeatureScript 2641;
import(path : "onshape/std/geometry.fs", version : "2641.0");

// =====================================================================
// ORBITAL COMPUTE STATION v5.0
// Two-track architecture: DP2 (bus) vs DP3 (spine) + Dragonfly clusters
//
// DP2: "Getting 140kW up there"
//   PV wings extend ±X from hub +Y face (sun-facing, thermally isolated)
//   Single radiator panel extends -Y from hub (SMAD 2.5:1 aspect)
//   Panel in XY plane, normal +Z (zenith/deep space), edge-on to sun
//   ORUs mounted directly on radiator +Z face — shortest thermal path
//
// DP3: "Scale through robotic assembly"
//   Spine along Y. PV at +Y, compute mid, radiator branches below
//   Cryogenic section for photonic compute (passive 150K in shadow)
//   All platforms in dawn-dusk SSO (~98% sunlight, no batteries needed)
//
// Clusters: Dragonfly topology (Kim, Dally, Abts 2008)
//   Center node = decode-biased, more laser terminals, ground antenna
//   Edge nodes = prefill/context-biased, more compute density
//   All-to-all optical links, diameter-3 routing
//
// Thermal: 370K radiator for all families (aggressive — requires future chips
//   with Tj,max > 400K or ultra-low-resistance thermal interfaces)
//   Gross: 0.96 * 5.67e-8 * 370^4 = 1021 W/m^2 per face
//   Earth IR backload at F=0.24: ~55 W/m^2
//   Net single-face: ~965 W/m^2. With sidedness 1.8: ~1750 W/m^2 effective
//   SMAD 0.75x rule: optimal Trad = 0.75 * Tj,max for min system mass
// =====================================================================

// --- Preset enum ---
export enum StationPreset
{
    annotation { "Name" : "DP2 - Rubin GPU-on-Radiator (140kW)" }
    DP2,
    annotation { "Name" : "DP2B - Centralized Hub (140kW)" }
    DP2B,
    annotation { "Name" : "DP3 - Photonic Spine Station (860kW)" }
    DP3,
    annotation { "Name" : "DP3B - AI7 Electrical Spine (1MW)" }
    DP3B,
    annotation { "Name" : "DP2 Cluster (1 center + 4 edge)" }
    DP2_CLUSTER,
    annotation { "Name" : "DP3 Cluster PH (1 center + 9 edge)" }
    DP3_CLUSTER_PH,
    annotation { "Name" : "DP3 Cluster EL (1 center + 9 edge)" }
    DP3_CLUSTER_EL
}

// --- Geometry constants ---
const PANEL_THICK  = 20 * millimeter;
const RAD_THICK    = 15 * millimeter;
const BB_W         = 50 * millimeter;
const BB_H         = 20 * millimeter;
const LINK_R       = 250 * millimeter;

// DP2 mechanical
const TRUSS_W      = 300 * millimeter;
const HINGE_R      = 80 * millimeter;
const HINGE_L      = 100 * millimeter;
const RCS_R        = 50 * millimeter;
const RCS_L        = 150 * millimeter;
const GIMBAL_R     = 120 * millimeter;

// DP3 spine
const SPINE_W      = 500 * millimeter;
const HEAT_PIPE_R  = 30 * millimeter;
// BRANCH_W, CRYOCOOLER_R/L deleted — branches removed, cryo uses fCuboid

// Cluster comm
const LASER_TERM_R = 150 * millimeter;
const LASER_TERM_L = 400 * millimeter;
const ANTENNA_R    = 1000 * millimeter;
const ANTENNA_DEPTH = 200 * millimeter;

// Structural dynamics stiffening
// Target: f1 > 0.1 Hz on all appendages (avoids ADCS coupling)
// ISS-style: longerons (spanwise beams) + battens (cross-members) + diagonal bracing
const LONGERON_W   = 60 * millimeter;   // longeron cross-section (square tube)
const LONGERON_H   = 60 * millimeter;
const BATTEN_W     = 40 * millimeter;   // batten cross-section
const BATTEN_H     = 40 * millimeter;
const BRACE_R      = 25 * millimeter;   // diagonal brace tube radius
const ROOT_BLOCK_W = 200 * millimeter;  // root stiffener block width
const ROOT_BLOCK_H = 150 * millimeter;  // root stiffener block height
const ROOT_BLOCK_D = 100 * millimeter;  // root stiffener block depth
const BATTEN_SPACING = 4.0;             // meters between battens (matches ISRA tile)
const DAMPER_R     = 40 * millimeter;   // tip mass damper radius
const DAMPER_L     = 200 * millimeter;  // tip mass damper length

// Texturing (heat pipe channels only — solar cell grid removed to avoid edge artifacts)
const PIPE_LINE_W  = 10 * millimeter;
const PIPE_LINE_H  = 4 * millimeter;

// --- Colors ---
const CLR_DECODE   = color(0.90, 0.25, 0.20);     // red — token generation
const CLR_PREFILL  = color(0.20, 0.45, 0.90);     // blue — parallel prompt processing
const CLR_CONTEXT  = color(0.20, 0.75, 0.30);     // green — KV cache / memory
const CLR_ROUTING  = color(0.90, 0.65, 0.10);     // orange — network routing (center node)
const CLR_COMPUTE  = color(0.75, 0.75, 0.78);
const CLR_PV       = color(0.05, 0.05, 0.55);     // deep blue solar
const CLR_RAD      = color(0.95, 0.95, 0.97);     // white radiator
const CLR_RAD_PIPE = color(0.85, 0.85, 0.90);     // slightly darker pipe channels
const CLR_HUB      = color(0.55, 0.55, 0.60);
const CLR_TRUSS    = color(0.70, 0.70, 0.75);
const CLR_GOLD     = color(0.85, 0.68, 0.10);
const CLR_LINK     = color(0.15, 0.80, 0.85);
const CLR_CRYO     = color(0.70, 0.85, 0.95);
const CLR_MLI      = color(0.95, 0.85, 0.20);
const CLR_PIPE     = color(0.50, 0.55, 0.60);
const CLR_RCS      = color(0.80, 0.80, 0.80);
const CLR_ANTENNA  = color(0.90, 0.90, 0.92);
const CLR_CENTER   = color(0.45, 0.45, 0.52);
// CLR_CABLE removed — guy wires eliminated per SpaceX structural approach

// =====================================================================
// NODE CONFIG
// =====================================================================
function getNodeConfig(preset is StationPreset, role is string, isCenter is boolean) returns map
{
    const m = meter;
    const mm = millimeter;

    var busPwr = 140.0;
    var computeKw = 128.8;
    var thermalKw = 140.0;
    var solarM2 = 558.14;
    var radM2 = 180.0;
    var radTempK = 370.0;
    var earthVF = 0.24;
    var sidedness = 1.80;
    var gpuPowerW = 1400.0;
    var oruW = 800 * mm;
    var oruH = 600 * mm;
    var oruD = 80 * mm;
    var hubScale = 1.0;
    var oruColor = CLR_COMPUTE;
    var centralized = false;
    var stationName = "DP2";
    var isDP3family = false;
    var isPhotonic = false;

    var spineLen = 90.0;
    var branchCount = 4;
    var branchLen = 20.0;

    if (preset == StationPreset.DP2)
    {
        stationName = "DP2 Rubin GPU-on-Radiator";
    }
    else if (preset == StationPreset.DP2B)
    {
        stationName = "DP2B Centralized Hub";
        centralized = true;
        hubScale = 1.8;
    }
    else if (preset == StationPreset.DP3 || preset == StationPreset.DP3_CLUSTER_PH)
    {
        stationName = "DP3 4K Photonic";
        // Right-sized for 4K photonic: ~283 kW total
        //   Warm electrical (I/O, memory, pre/post): 200 kW
        //   Cryocooler (100W @ 4K, COP ~0.003): 33 kW
        //   Housekeeping (ADCS, comms, thermal): 50 kW
        // Photonic advantage: massive compute per watt (superconducting)
        busPwr = 283.0;
        computeKw = 200.0;   // warm electrical only (photonic is ~100W at 4K)
        thermalKw = 283.0;
        solarM2 = 1100.0;   // 283kW / (1361 * 0.30 * 0.90) × 1.6 margin
        radM2 = 333.0;      // 2.0x over 166 m² thermal min at 370K
        radTempK = 370.0;
        earthVF = 0.22;
        sidedness = 1.72;
        gpuPowerW = 200.0;  // warm I/O + memory ORUs, not photonic chips
        oruW = 500 * mm;
        oruH = 400 * mm;
        oruD = 40 * mm;
        hubScale = 1.5;
        isDP3family = true;
        isPhotonic = true;
        spineLen = 60.0;    // PV → radiator → cryo chain (needs ~55m)
        branchCount = 1;    // single radiator (scale via cluster, not station)
        branchLen = 0.0;
    }
    else if (preset == StationPreset.DP3B || preset == StationPreset.DP3_CLUSTER_EL)
    {
        stationName = "DP3B AI7 Electrical Spine";
        busPwr = 1000.0;
        computeKw = 940.0;
        thermalKw = 1000.0;
        solarM2 = 3986.73;
        radM2 = 1200.0;   // 2.0x over 589 m² thermal min at 370K
        radTempK = 370.0;
        earthVF = 0.22;
        sidedness = 1.72;
        gpuPowerW = 250.0;
        oruW = 500 * mm;
        oruH = 400 * mm;
        oruD = 40 * mm;
        hubScale = 2.3;
        isDP3family = true;
        isPhotonic = false;
        spineLen = 30.0;   // no spine compute → short: PV → gap → radiator
        branchCount = 1;   // single radiator panel at -Y
        branchLen = 0.0;
    }
    else if (preset == StationPreset.DP2_CLUSTER)
    {
        stationName = "DP2 Cluster Node";
    }

    // Biased role dedication: each node does ALL roles, just biased toward one
    // Primary role gets ~60% of ORUs, secondary ~25%, tertiary ~15%
    var oruColor2 = CLR_COMPUTE;
    var oruColor3 = CLR_COMPUTE;

    if (role == "routing")
    {
        // Center node: router that also does compute
        // More comm hardware, biased toward serving/routing
        oruColor = CLR_ROUTING;
        oruColor2 = CLR_DECODE;
        oruColor3 = CLR_PREFILL;
        stationName = stationName ~ " [ROUTING/SERVING]";
    }
    else if (role == "decode")
    {
        oruColor = CLR_DECODE;
        oruColor2 = CLR_PREFILL;
        oruColor3 = CLR_CONTEXT;
        stationName = stationName ~ " [DECODE-biased]";
    }
    else if (role == "prefill")
    {
        oruColor = CLR_PREFILL;
        oruColor2 = CLR_DECODE;
        oruColor3 = CLR_CONTEXT;
        stationName = stationName ~ " [PREFILL-biased]";
    }
    else if (role == "context")
    {
        oruColor = CLR_CONTEXT;
        oruColor2 = CLR_PREFILL;
        oruColor3 = CLR_DECODE;
        stationName = stationName ~ " [CONTEXT-biased]";
    }

    if (isCenter)
    {
        stationName = stationName ~ " (CENTER)";
    }

    var oruCount = 0;
    if (!centralized)
    {
        oruCount = floor(computeKw * 1000.0 / gpuPowerW);
    }
    if (oruCount > 500)
    {
        oruCount = 300;
    }

    // PV wing dimensions (single wing for DP2, two wings for DP3)
    const pvChord = sqrt(solarM2 / 2.0 / 1.5);
    const pvSpan = 1.5 * pvChord;

    // DP2: single continuous radiator panel (SMAD: fewer mechanisms, one deployment)
    // SMAD aspect ratio 2.5:1 (span:chord) for structural rigidity
    // SpaceX step 2: delete the part — 1 panel not 2
    const radPanelChord = sqrt(radM2 / 2.5);
    const radPanelSpan = 2.5 * radPanelChord;

    // DP3: modular radiator panels along spine (comb layout)
    // ISRA: standard modules robotically assembled, growable over time
    // Panels in XZ plane (normal Y), all facing -Y (away from sun)
    const totalPanels = branchCount;
    var radPerPanel = 0.0;
    if (totalPanels > 0)
    {
        radPerPanel = radM2 / (totalPanels * 1.0);
    }
    // Aspect 2.5:1 (wide in X : narrow in Z) — extends ±X like PV wings
    var branchPanelW = sqrt(radPerPanel * 2.5);   // X span (wide)
    var branchPanelH = sqrt(radPerPanel / 2.5);   // Z span (narrow)

    const hubX = 1200 * mm * hubScale;
    const hubY = 800 * mm * hubScale;
    const hubZ = 600 * mm * hubScale;

    var oruPerPanel = 0;
    if (!centralized && oruCount > 0)
    {
        if (isDP3family)
        {
            if (totalPanels > 0)
            {
                oruPerPanel = floor(oruCount / totalPanels);
            }
        }
        else
        {
            // DP2: single radiator panel, ORUs on +Z face (deep space) only
            oruPerPanel = oruCount;
        }
    }
    if (oruPerPanel < 1 && oruCount > 0)
    {
        oruPerPanel = 1;
    }
    // Aspect-ratio-aware grid: match panel shape so ORUs tile evenly
    // For a panel with aspect ratio A (height/width), rows/cols ≈ sqrt(N*A) / sqrt(N/A)
    var panelAspect = 2.5;  // default for both DP2 and DP3
    var oruCols = floor(sqrt(oruPerPanel * 1.0 / panelAspect));
    if (oruCols < 1)
    {
        oruCols = 1;
    }
    var oruRows = floor(oruPerPanel / oruCols);
    if (oruRows < 1)
    {
        oruRows = 1;
    }

    return {
        "name" : stationName,
        "busPwr" : busPwr, "computeKw" : computeKw, "thermalKw" : thermalKw,
        "solarM2" : solarM2, "radM2" : radM2, "radTempK" : radTempK,
        "earthVF" : earthVF, "sidedness" : sidedness,
        "pvSpan" : pvSpan * m, "pvChord" : pvChord * m,
        "radPanelSpan" : radPanelSpan * m, "radPanelChord" : radPanelChord * m,
        "hubX" : hubX, "hubY" : hubY, "hubZ" : hubZ,
        "oruW" : oruW, "oruH" : oruH, "oruD" : oruD,
        "oruCount" : oruCount, "oruPerPanel" : oruPerPanel,
        "oruCols" : oruCols, "oruRows" : oruRows,
        "oruColor" : oruColor, "oruColor2" : oruColor2, "oruColor3" : oruColor3,
        "centralized" : centralized,
        "gpuPowerW" : gpuPowerW,
        "isDP3family" : isDP3family, "isPhotonic" : isPhotonic,
        "isCenter" : isCenter,
        "spineLen" : spineLen * m, "branchCount" : branchCount,
        "branchLen" : branchLen * m,
        "branchPanelW" : branchPanelW * m, "branchPanelH" : branchPanelH * m,
        "totalPanels" : totalPanels
    };
}

// =====================================================================
// TEXTURING HELPERS
// =====================================================================

// Heat pipe channel lines on a radiator panel
// Panel in YZ plane (normal along X), center given
function addRadiatorPipeLines(context is Context, id is Id,
                              panelCenter is Vector, panelW, panelH,
                              normalAxis is string, nLines is number)
{
    for (var i = 0; i < nLines; i += 1)
    {
        var lineId = "rpl" ~ i;
        if (normalAxis == "X")
        {
            // Panel in YZ plane, pipes run along Z (vertical)
            var yPos = -panelW / 2 + panelW * (i + 1) / (nLines + 1);
            fCuboid(context, id + lineId, {
                    "corner1" : panelCenter + vector(RAD_THICK / 2, yPos - PIPE_LINE_W / 2, -panelH / 2),
                    "corner2" : panelCenter + vector(RAD_THICK / 2 + PIPE_LINE_H, yPos + PIPE_LINE_W / 2, panelH / 2)
            });
        }
        else if (normalAxis == "Y")
        {
            // Panel in XZ plane (normal Y), pipes run along Z
            var xPos = -panelW / 2 + panelW * (i + 1) / (nLines + 1);
            fCuboid(context, id + lineId, {
                    "corner1" : panelCenter + vector(xPos - PIPE_LINE_W / 2, RAD_THICK / 2, -panelH / 2),
                    "corner2" : panelCenter + vector(xPos + PIPE_LINE_W / 2, RAD_THICK / 2 + PIPE_LINE_H, panelH / 2)
            });
        }
        else
        {
            // Panel in XY plane (normal Z), pipes run along Y
            var xPos = -panelW / 2 + panelW * (i + 1) / (nLines + 1);
            fCuboid(context, id + lineId, {
                    "corner1" : panelCenter + vector(xPos - PIPE_LINE_W / 2, -panelH / 2, RAD_THICK / 2),
                    "corner2" : panelCenter + vector(xPos + PIPE_LINE_W / 2, panelH / 2, RAD_THICK / 2 + PIPE_LINE_H)
            });
        }
        setProperty(context, { "entities" : qCreatedBy(id + lineId, EntityType.BODY),
                "propertyType" : PropertyType.APPEARANCE, "value" : CLR_RAD_PIPE });
        setProperty(context, { "entities" : qCreatedBy(id + lineId, EntityType.BODY),
                "propertyType" : PropertyType.NAME, "value" : "Heat Pipe Channel" });
    }
}

// =====================================================================
// GEOMETRY HELPERS
// =====================================================================

function buildRadPanel(context is Context, id is Id, center is Vector,
                       normal is Vector, w, h, panelName is string)
{
    const halfT = RAD_THICK / 2;
    if (abs(normal[0]) > 0.5)
    {
        fCuboid(context, id, {
                "corner1" : center + vector(-halfT, -w / 2, -h / 2),
                "corner2" : center + vector( halfT,  w / 2,  h / 2)
        });
    }
    else if (abs(normal[2]) > 0.5)
    {
        fCuboid(context, id, {
                "corner1" : center + vector(-w / 2, -h / 2, -halfT),
                "corner2" : center + vector( w / 2,  h / 2,  halfT)
        });
    }
    else
    {
        fCuboid(context, id, {
                "corner1" : center + vector(-w / 2, -halfT, -h / 2),
                "corner2" : center + vector( w / 2,  halfT,  h / 2)
        });
    }
    setProperty(context, { "entities" : qCreatedBy(id, EntityType.BODY),
            "propertyType" : PropertyType.APPEARANCE, "value" : CLR_RAD });
    setProperty(context, { "entities" : qCreatedBy(id, EntityType.BODY),
            "propertyType" : PropertyType.NAME, "value" : panelName });
}

function buildHinge(context is Context, id is Id, pos is Vector, axis is Vector)
{
    fCylinder(context, id, {
            "bottomCenter" : pos - axis * (HINGE_L / 2),
            "topCenter" : pos + axis * (HINGE_L / 2),
            "radius" : HINGE_R
    });
    setProperty(context, { "entities" : qCreatedBy(id, EntityType.BODY),
            "propertyType" : PropertyType.APPEARANCE, "value" : CLR_PIPE });
    setProperty(context, { "entities" : qCreatedBy(id, EntityType.BODY),
            "propertyType" : PropertyType.NAME, "value" : "Deployment Hinge" });
}

function buildRCSPod(context is Context, id is Id, pos is Vector, dir is Vector)
{
    fCylinder(context, id, {
            "bottomCenter" : pos,
            "topCenter" : pos + dir * RCS_L,
            "radius" : RCS_R
    });
    setProperty(context, { "entities" : qCreatedBy(id, EntityType.BODY),
            "propertyType" : PropertyType.APPEARANCE, "value" : CLR_RCS });
    setProperty(context, { "entities" : qCreatedBy(id, EntityType.BODY),
            "propertyType" : PropertyType.NAME, "value" : "RCS Thruster" });
}

// buildThermalStrap removed — ORU-on-radiator IS the thermal path (SpaceX: best part is no part)

// =====================================================================
// STRUCTURAL DYNAMICS STIFFENING
// =====================================================================
// ISS solar array architecture: longerons (spanwise beams running the full
// length of each wing), battens (cross-members at regular intervals), and
// diagonal braces (tension/compression members for torsional stiffness).
//
// First-mode frequency check (cantilever beam):
//   f1 = (1.875²/2π) × sqrt(EI / ρAL⁴)
//   For CFRP longeron (E=150 GPa, 60mm sq tube, wall 3mm):
//     I = 5.3e-7 m⁴, A = 6.84e-4 m², ρ = 1600 kg/m³
//     L = 14m (DP2 half-span): f1 ≈ 0.35 Hz ✓ (well above 0.1 Hz)
//     L = 19m (DP3 half-span): f1 ≈ 0.14 Hz ✓ (marginal but OK with battens)
//
// Battens add torsional rigidity — prevent wing twist from solar pressure
// torques. Spacing at 4m matches ISRA tile modules for manufacturing.
//
// Root stiffener blocks transfer bending moment into hub/spine without
// stress concentration at the joint.
//
// Tip mass dampers (optional, showMech) — passive vibration suppression
// at wing tips to damp first-mode oscillation after maneuvers.

// Longerons + battens along a wing/panel extending from a root point
// wingAxis: unit vector along span (direction wing extends)
// chordAxis: unit vector along chord (perpendicular to span, in panel plane)
// normalAxis: unit vector normal to panel (perpendicular to both)
function buildWingStructure(context is Context, id is Id,
                            rootCenter is Vector, wingSpan, wingChord,
                            wingAxis is Vector, chordAxis is Vector,
                            normalAxis is Vector, showDamper is boolean)
{
    const m = meter;

    // Two longerons offset ±30% chord from centerline
    // Running full span underneath the panel (on -normal side)
    for (var li = 0; li < 2; li += 1)
    {
        var lId = "lgn" ~ li;
        var chordOff = (-0.3 + 0.6 * li) * wingChord;
        var lStart = rootCenter + chordAxis * chordOff - normalAxis * (PANEL_THICK / 2 + LONGERON_H / 2);
        var lEnd = lStart + wingAxis * wingSpan;

        // Longeron as cuboid along wingAxis
        var c1 = lStart - chordAxis * (LONGERON_W / 2) - normalAxis * (LONGERON_H / 2);
        var c2 = lEnd + chordAxis * (LONGERON_W / 2) + normalAxis * (LONGERON_H / 2);

        // Need to express in XYZ — build from component offsets
        fCuboid(context, id + lId, { "corner1" : c1, "corner2" : c2 });
        setProperty(context, { "entities" : qCreatedBy(id + lId, EntityType.BODY),
                "propertyType" : PropertyType.APPEARANCE, "value" : CLR_TRUSS });
        setProperty(context, { "entities" : qCreatedBy(id + lId, EntityType.BODY),
                "propertyType" : PropertyType.NAME, "value" : "Longeron " ~ li });
    }

    // Battens — cross-members connecting the two longerons every BATTEN_SPACING meters
    var battenCount = floor(wingSpan / (BATTEN_SPACING * m));
    if (battenCount < 1)
    {
        battenCount = 1;
    }
    for (var bi = 0; bi < battenCount; bi += 1)
    {
        var bId = "bat" ~ bi;
        var spanPos = (bi + 1) * BATTEN_SPACING * m;
        if (spanPos > wingSpan - 0.5 * m)
        {
            spanPos = wingSpan - 0.5 * m;
        }
        var bCenter = rootCenter + wingAxis * spanPos - normalAxis * (PANEL_THICK / 2 + LONGERON_H / 2);
        var bStart = bCenter - chordAxis * (0.3 * wingChord);
        var bEnd = bCenter + chordAxis * (0.3 * wingChord);

        var bc1 = bStart - wingAxis * (BATTEN_W / 2) - normalAxis * (BATTEN_H / 2);
        var bc2 = bEnd + wingAxis * (BATTEN_W / 2) + normalAxis * (BATTEN_H / 2);

        fCuboid(context, id + bId, { "corner1" : bc1, "corner2" : bc2 });
        setProperty(context, { "entities" : qCreatedBy(id + bId, EntityType.BODY),
                "propertyType" : PropertyType.APPEARANCE, "value" : CLR_TRUSS });
        setProperty(context, { "entities" : qCreatedBy(id + bId, EntityType.BODY),
                "propertyType" : PropertyType.NAME, "value" : "Batten " ~ bi });
    }

    // Root stiffener block — transfers bending moment into hub/spine
    var rCenter = rootCenter - normalAxis * (PANEL_THICK / 2 + ROOT_BLOCK_H / 2);
    fCuboid(context, id + "root", {
            "corner1" : rCenter - chordAxis * (ROOT_BLOCK_W / 2) - wingAxis * (ROOT_BLOCK_D / 2) - normalAxis * (ROOT_BLOCK_H / 2),
            "corner2" : rCenter + chordAxis * (ROOT_BLOCK_W / 2) + wingAxis * (ROOT_BLOCK_D / 2) + normalAxis * (ROOT_BLOCK_H / 2)
    });
    setProperty(context, { "entities" : qCreatedBy(id + "root", EntityType.BODY),
            "propertyType" : PropertyType.APPEARANCE, "value" : CLR_PIPE });
    setProperty(context, { "entities" : qCreatedBy(id + "root", EntityType.BODY),
            "propertyType" : PropertyType.NAME, "value" : "Root Stiffener" });

    // Tip mass damper — passive vibration suppression (optional)
    if (showDamper)
    {
        var tipCenter = rootCenter + wingAxis * wingSpan - normalAxis * (PANEL_THICK / 2 + LONGERON_H);
        fCylinder(context, id + "dmp", {
                "bottomCenter" : tipCenter - chordAxis * (DAMPER_L / 2),
                "topCenter" : tipCenter + chordAxis * (DAMPER_L / 2),
                "radius" : DAMPER_R
        });
        setProperty(context, { "entities" : qCreatedBy(id + "dmp", EntityType.BODY),
                "propertyType" : PropertyType.APPEARANCE, "value" : CLR_RCS });
        setProperty(context, { "entities" : qCreatedBy(id + "dmp", EntityType.BODY),
                "propertyType" : PropertyType.NAME, "value" : "Tip Mass Damper" });
    }
}

// Spine cross-bracing — diagonal tubes in X-pattern between spine faces
// Prevents torsional modes and lateral bending of long spines
// pattern: alternating X-braces every segLen meters along spine
function buildSpineBracing(context is Context, id is Id, offset is Vector,
                           spineBottom, spineTop, segLen)
{
    const m = meter;
    const halfW = SPINE_W / 2 + BRACE_R;  // offset from spine center
    var nSegs = floor((spineTop - spineBottom) / (segLen * m));
    if (nSegs < 1)
    {
        nSegs = 1;
    }
    var segH = (spineTop - spineBottom) / nSegs;

    for (var si = 0; si < nSegs; si += 1)
    {
        var yBot = spineBottom + si * segH;
        var yTop = yBot + segH;

        // X-brace in XY plane (±X face of spine)
        // Diagonal 1: bottom-left to top-right
        fCylinder(context, id + ("bxA" ~ si), {
                "bottomCenter" : offset + vector(halfW, yBot, 0 * m),
                "topCenter" : offset + vector(-halfW, yTop, 0 * m),
                "radius" : BRACE_R
        });
        setProperty(context, { "entities" : qCreatedBy(id + ("bxA" ~ si), EntityType.BODY),
                "propertyType" : PropertyType.APPEARANCE, "value" : CLR_TRUSS });
        setProperty(context, { "entities" : qCreatedBy(id + ("bxA" ~ si), EntityType.BODY),
                "propertyType" : PropertyType.NAME, "value" : "X-Brace" });

        // Diagonal 2: bottom-right to top-left
        fCylinder(context, id + ("bxB" ~ si), {
                "bottomCenter" : offset + vector(-halfW, yBot, 0 * m),
                "topCenter" : offset + vector(halfW, yTop, 0 * m),
                "radius" : BRACE_R
        });
        setProperty(context, { "entities" : qCreatedBy(id + ("bxB" ~ si), EntityType.BODY),
                "propertyType" : PropertyType.APPEARANCE, "value" : CLR_TRUSS });
        setProperty(context, { "entities" : qCreatedBy(id + ("bxB" ~ si), EntityType.BODY),
                "propertyType" : PropertyType.NAME, "value" : "X-Brace" });

        // X-brace in ZY plane (±Z face of spine)
        fCylinder(context, id + ("bzA" ~ si), {
                "bottomCenter" : offset + vector(0 * m, yBot, halfW),
                "topCenter" : offset + vector(0 * m, yTop, -halfW),
                "radius" : BRACE_R
        });
        setProperty(context, { "entities" : qCreatedBy(id + ("bzA" ~ si), EntityType.BODY),
                "propertyType" : PropertyType.APPEARANCE, "value" : CLR_TRUSS });
        setProperty(context, { "entities" : qCreatedBy(id + ("bzA" ~ si), EntityType.BODY),
                "propertyType" : PropertyType.NAME, "value" : "X-Brace" });

        fCylinder(context, id + ("bzB" ~ si), {
                "bottomCenter" : offset + vector(0 * m, yBot, -halfW),
                "topCenter" : offset + vector(0 * m, yTop, halfW),
                "radius" : BRACE_R
        });
        setProperty(context, { "entities" : qCreatedBy(id + ("bzB" ~ si), EntityType.BODY),
                "propertyType" : PropertyType.APPEARANCE, "value" : CLR_TRUSS });
        setProperty(context, { "entities" : qCreatedBy(id + ("bzB" ~ si), EntityType.BODY),
                "propertyType" : PropertyType.NAME, "value" : "X-Brace" });
    }
}

// Radiator panel stiffening — longerons along the long axis of a radiator panel
// Fewer members than PV wings since radiator is stiffer (aluminum honeycomb)
// but still needs bending stiffness for large panels
function buildRadiatorStiffening(context is Context, id is Id,
                                  panelCenter is Vector, panelW, panelH,
                                  normalAxis is string)
{
    // Two longerons at ±25% of the narrow dimension
    // Running along the long dimension
    for (var li = 0; li < 2; li += 1)
    {
        var lId = "rlg" ~ li;
        var offFrac = -0.25 + 0.5 * li;

        if (normalAxis == "Z")
        {
            // Panel in XY plane (normal Z) — long axis is Y (span), narrow X (chord)
            var zOff = -(RAD_THICK / 2 + LONGERON_H / 2);
            var xOff = offFrac * panelW;
            fCuboid(context, id + lId, {
                    "corner1" : panelCenter + vector(xOff - LONGERON_W / 2, -panelH / 2, zOff - LONGERON_H / 2),
                    "corner2" : panelCenter + vector(xOff + LONGERON_W / 2,  panelH / 2, zOff + LONGERON_H / 2)
            });
        }
        else if (normalAxis == "X")
        {
            // Panel in YZ plane (normal X) — long axis is Y (height), narrow Z (width)
            var xOff = -(RAD_THICK / 2 + LONGERON_H / 2);
            var zOff = offFrac * panelH;
            fCuboid(context, id + lId, {
                    "corner1" : panelCenter + vector(xOff - LONGERON_H / 2, -panelW / 2, zOff - LONGERON_W / 2),
                    "corner2" : panelCenter + vector(xOff + LONGERON_H / 2,  panelW / 2, zOff + LONGERON_W / 2)
            });
        }
        else
        {
            // Panel in XZ plane (normal Y) — long axis is X (width), narrow Z (height)
            var yOff = -(RAD_THICK / 2 + LONGERON_H / 2);
            var zOff = offFrac * panelH;
            fCuboid(context, id + lId, {
                    "corner1" : panelCenter + vector(-panelW / 2, yOff - LONGERON_H / 2, zOff - LONGERON_W / 2),
                    "corner2" : panelCenter + vector( panelW / 2, yOff + LONGERON_H / 2, zOff + LONGERON_W / 2)
            });
        }
        setProperty(context, { "entities" : qCreatedBy(id + lId, EntityType.BODY),
                "propertyType" : PropertyType.APPEARANCE, "value" : CLR_TRUSS });
        setProperty(context, { "entities" : qCreatedBy(id + lId, EntityType.BODY),
                "propertyType" : PropertyType.NAME, "value" : "Radiator Longeron " ~ li });
    }
}

function buildLaserTerminal(context is Context, id is Id, pos is Vector, dir is Vector)
{
    fCylinder(context, id, {
            "bottomCenter" : pos,
            "topCenter" : pos + dir * LASER_TERM_L,
            "radius" : LASER_TERM_R
    });
    setProperty(context, { "entities" : qCreatedBy(id, EntityType.BODY),
            "propertyType" : PropertyType.APPEARANCE, "value" : CLR_ANTENNA });
    setProperty(context, { "entities" : qCreatedBy(id, EntityType.BODY),
            "propertyType" : PropertyType.NAME, "value" : "Laser Comm Terminal" });
}

// ORUs on one face of a radiator panel
// Supports biased role dedication: primary (60%), secondary (25%), tertiary (15%)
// Colors scattered via modulo so the mix is visible across the panel
function buildORUsOnPanel(context is Context, id is Id, panelCenter is Vector,
                          panelW, panelH, normalAxis is string,
                          oruW, oruH, oruD, oruCols is number, oruRows is number,
                          oruColor, oruColor2, oruColor3, faceSide is number)
{
    if (oruCols < 1 || oruRows < 1)
    {
        return;
    }

    const spW = panelW / (oruCols + 1);
    const spH = panelH / (oruRows + 1);

    for (var ci = 0; ci < oruCols; ci += 1)
    {
        for (var ri = 0; ri < oruRows; ri += 1)
        {
            var oruId = "u" ~ faceSide ~ "_" ~ ci ~ "_" ~ ri;
            var localW = -panelW / 2 + spW * (ci + 1);
            var localH = -panelH / 2 + spH * (ri + 1);

            if (normalAxis == "X")
            {
                // Panel in YZ, ORU offset along X
                var xOff = (RAD_THICK / 2 + oruD / 2) * faceSide;
                var c = panelCenter + vector(xOff, localW, localH);
                fCuboid(context, id + oruId, {
                        "corner1" : c + vector(-oruD / 2, -oruW / 2, -oruH / 2),
                        "corner2" : c + vector( oruD / 2,  oruW / 2,  oruH / 2)
                });
            }
            else if (normalAxis == "Y")
            {
                // Panel in XZ, ORU offset along Y
                var yOff = (RAD_THICK / 2 + oruD / 2) * faceSide;
                var c = panelCenter + vector(localW, yOff, localH);
                fCuboid(context, id + oruId, {
                        "corner1" : c + vector(-oruW / 2, -oruD / 2, -oruH / 2),
                        "corner2" : c + vector( oruW / 2,  oruD / 2,  oruH / 2)
                });
            }
            else
            {
                // Panel in XY (normal Z), ORU offset along Z
                var zOff = (RAD_THICK / 2 + oruD / 2) * faceSide;
                var c = panelCenter + vector(localW, localH, zOff);
                fCuboid(context, id + oruId, {
                        "corner1" : c + vector(-oruW / 2, -oruH / 2, -oruD / 2),
                        "corner2" : c + vector( oruW / 2,  oruH / 2,  oruD / 2)
                });
            }

            // Biased role color: LCG hash scatter (no visible banding)
            // bucket 0-11 (60%) primary, 12-16 (25%) secondary, 17-19 (15%) tertiary
            var oruIdx = ci * oruRows + ri;
            var bucket = (oruIdx * 7 + 3) % 20;
            var thisColor = oruColor;
            if (bucket >= 12 && bucket < 17)
            {
                thisColor = oruColor2;
            }
            else if (bucket >= 17)
            {
                thisColor = oruColor3;
            }

            setProperty(context, { "entities" : qCreatedBy(id + oruId, EntityType.BODY),
                    "propertyType" : PropertyType.APPEARANCE, "value" : thisColor });
            setProperty(context, { "entities" : qCreatedBy(id + oruId, EntityType.BODY),
                    "propertyType" : PropertyType.NAME, "value" : "ORU" });
        }
    }
}

// =====================================================================
// BUILD DP2 NODE
// Layout: SUN → [PV wing +Y] → [HUB] → [Radiator panels -Y] → DEEP SPACE
// Solar on one side, radiator on the other
// =====================================================================
function buildNodeDP2(context is Context, id is Id, cfg is map,
                      offset is Vector, showORUs is boolean,
                      showTruss is boolean, showBusBar is boolean,
                      showMech is boolean)
{
    // --- Hub ---
    var hubClr = CLR_HUB;
    if (cfg.isCenter)
    {
        hubClr = CLR_CENTER;
    }
    // Main bus structure
    fCuboid(context, id + "hub", {
            "corner1" : offset + vector(-cfg.hubX / 2, -cfg.hubY / 2, -cfg.hubZ / 2),
            "corner2" : offset + vector( cfg.hubX / 2,  cfg.hubY / 2,  cfg.hubZ / 2)
    });
    setProperty(context, { "entities" : qCreatedBy(id + "hub", EntityType.BODY),
            "propertyType" : PropertyType.APPEARANCE, "value" : hubClr });
    setProperty(context, { "entities" : qCreatedBy(id + "hub", EntityType.BODY),
            "propertyType" : PropertyType.NAME, "value" : cfg.name ~ " Bus" });

    // PMAD (Power Management & Distribution) — top of hub, gold MLI
    const pmadH = cfg.hubZ * 0.25;
    fCuboid(context, id + "pmad", {
            "corner1" : offset + vector(-cfg.hubX * 0.4, -cfg.hubY * 0.4, cfg.hubZ / 2),
            "corner2" : offset + vector( cfg.hubX * 0.4,  cfg.hubY * 0.4, cfg.hubZ / 2 + pmadH)
    });
    setProperty(context, { "entities" : qCreatedBy(id + "pmad", EntityType.BODY),
            "propertyType" : PropertyType.APPEARANCE, "value" : CLR_GOLD });
    setProperty(context, { "entities" : qCreatedBy(id + "pmad", EntityType.BODY),
            "propertyType" : PropertyType.NAME, "value" : "PMAD Unit" });

    // Avionics bay — small box on -Z face
    const avH = cfg.hubZ * 0.2;
    fCuboid(context, id + "avio", {
            "corner1" : offset + vector(-cfg.hubX * 0.25, -cfg.hubY * 0.25, -cfg.hubZ / 2 - avH),
            "corner2" : offset + vector( cfg.hubX * 0.25,  cfg.hubY * 0.25, -cfg.hubZ / 2)
    });
    setProperty(context, { "entities" : qCreatedBy(id + "avio", EntityType.BODY),
            "propertyType" : PropertyType.APPEARANCE, "value" : CLR_CENTER });
    setProperty(context, { "entities" : qCreatedBy(id + "avio", EntityType.BODY),
            "propertyType" : PropertyType.NAME, "value" : "Avionics (ADCS + Flight Computer)" });

    // Ground link antenna — dish on +X face
    fCylinder(context, id + "gAnt", {
            "bottomCenter" : offset + vector(cfg.hubX / 2, 0 * meter, 0 * meter),
            "topCenter" : offset + vector(cfg.hubX / 2 + ANTENNA_DEPTH, 0 * meter, 0 * meter),
            "radius" : ANTENNA_R * 0.6
    });
    setProperty(context, { "entities" : qCreatedBy(id + "gAnt", EntityType.BODY),
            "propertyType" : PropertyType.APPEARANCE, "value" : CLR_ANTENNA });
    setProperty(context, { "entities" : qCreatedBy(id + "gAnt", EntityType.BODY),
            "propertyType" : PropertyType.NAME, "value" : "Ground Link Antenna" });

    // --- Centralized compute bay (DP2B only) ---
    if (cfg.centralized)
    {
        const bayInset = 100 * millimeter;
        fCuboid(context, id + "cBay", {
                "corner1" : offset + vector(-cfg.hubX / 2 + bayInset, -cfg.hubY / 2 + bayInset, -cfg.hubZ / 2 + bayInset),
                "corner2" : offset + vector( cfg.hubX / 2 - bayInset,  cfg.hubY / 2 - bayInset,  cfg.hubZ / 2 - bayInset)
        });
        setProperty(context, { "entities" : qCreatedBy(id + "cBay", EntityType.BODY),
                "propertyType" : PropertyType.APPEARANCE, "value" : cfg.oruColor });
        setProperty(context, { "entities" : qCreatedBy(id + "cBay", EntityType.BODY),
                "propertyType" : PropertyType.NAME, "value" : "Centralized Compute Bay" });
    }

    // --- PV Wings extending ±X from hub +Y face ---
    // Dual-function panels: blue sun-facing PV cells + white anti-sun radiator back
    // Thermally isolated from compute radiator — PV stays cool for max efficiency
    const pvYCenter = cfg.hubY / 2 + PANEL_THICK / 2;
    const pvHalfT = PANEL_THICK / 2;

    // +X PV wing — sun-facing layer (blue PV cells)
    fCuboid(context, id + "pvPF", {
            "corner1" : offset + vector(0 * meter, pvYCenter, -cfg.pvChord / 2),
            "corner2" : offset + vector(cfg.pvSpan, pvYCenter + pvHalfT, cfg.pvChord / 2)
    });
    setProperty(context, { "entities" : qCreatedBy(id + "pvPF", EntityType.BODY),
            "propertyType" : PropertyType.APPEARANCE, "value" : CLR_PV });
    setProperty(context, { "entities" : qCreatedBy(id + "pvPF", EntityType.BODY),
            "propertyType" : PropertyType.NAME, "value" : "PV Wing +X (cells)" });

    // +X PV wing — anti-sun layer (white radiator back)
    fCuboid(context, id + "pvPB", {
            "corner1" : offset + vector(0 * meter, pvYCenter - pvHalfT, -cfg.pvChord / 2),
            "corner2" : offset + vector(cfg.pvSpan, pvYCenter, cfg.pvChord / 2)
    });
    setProperty(context, { "entities" : qCreatedBy(id + "pvPB", EntityType.BODY),
            "propertyType" : PropertyType.APPEARANCE, "value" : CLR_RAD });
    setProperty(context, { "entities" : qCreatedBy(id + "pvPB", EntityType.BODY),
            "propertyType" : PropertyType.NAME, "value" : "PV Wing +X (radiator back)" });

    // -X PV wing — sun-facing layer (blue PV cells)
    fCuboid(context, id + "pvNF", {
            "corner1" : offset + vector(-cfg.pvSpan, pvYCenter, -cfg.pvChord / 2),
            "corner2" : offset + vector(0 * meter, pvYCenter + pvHalfT, cfg.pvChord / 2)
    });
    setProperty(context, { "entities" : qCreatedBy(id + "pvNF", EntityType.BODY),
            "propertyType" : PropertyType.APPEARANCE, "value" : CLR_PV });
    setProperty(context, { "entities" : qCreatedBy(id + "pvNF", EntityType.BODY),
            "propertyType" : PropertyType.NAME, "value" : "PV Wing -X (cells)" });

    // -X PV wing — anti-sun layer (white radiator back)
    fCuboid(context, id + "pvNB", {
            "corner1" : offset + vector(-cfg.pvSpan, pvYCenter - pvHalfT, -cfg.pvChord / 2),
            "corner2" : offset + vector(0 * meter, pvYCenter, cfg.pvChord / 2)
    });
    setProperty(context, { "entities" : qCreatedBy(id + "pvNB", EntityType.BODY),
            "propertyType" : PropertyType.APPEARANCE, "value" : CLR_RAD });
    setProperty(context, { "entities" : qCreatedBy(id + "pvNB", EntityType.BODY),
            "propertyType" : PropertyType.NAME, "value" : "PV Wing -X (radiator back)" });

    // --- PV wing structural stiffening ---
    // Longerons + battens underneath each wing prevent floppy first-mode oscillation
    // f1 target > 0.1 Hz for ADCS decoupling
    // +X wing: root at hub edge, extends along +X
    var pvWingRoot = offset + vector(0 * meter, pvYCenter, 0 * meter);
    buildWingStructure(context, id + "wsP", pvWingRoot, cfg.pvSpan, cfg.pvChord,
                       vector(1, 0, 0), vector(0, 0, 1), vector(0, 1, 0), showMech);
    // -X wing: root at hub edge, extends along -X
    buildWingStructure(context, id + "wsN", pvWingRoot, cfg.pvSpan, cfg.pvChord,
                       vector(-1, 0, 0), vector(0, 0, 1), vector(0, 1, 0), showMech);

    // --- Gimbal fairings at PV junctions ---
    if (showMech)
    {
        fCylinder(context, id + "gimP", {
                "bottomCenter" : offset + vector(0 * meter, cfg.hubY / 2 - 50 * millimeter, 0 * meter),
                "topCenter" : offset + vector(0 * meter, cfg.hubY / 2 + 100 * millimeter, 0 * meter),
                "radius" : GIMBAL_R
        });
        setProperty(context, { "entities" : qCreatedBy(id + "gimP", EntityType.BODY),
                "propertyType" : PropertyType.APPEARANCE, "value" : CLR_PIPE });
        setProperty(context, { "entities" : qCreatedBy(id + "gimP", EntityType.BODY),
                "propertyType" : PropertyType.NAME, "value" : "PV Gimbal" });
    }

    // --- Single Continuous Radiator Panel extending -Y from hub ---
    // SpaceX step 2: delete the part — 1 panel, 1 hinge, 1 deployment
    // SMAD 2.5:1 aspect ratio for structural rigidity
    // In XY plane, normal +Z (faces zenith/deep space)
    // 180 m² at 370K, sidedness 1.8: ~295 kW capacity for 140 kW load (2.1× margin)
    // Chord ~8.5m (fits 9m Starship fairing), span ~21m (accordion-folds to ~7m stowed)
    const panelGap = 0.3 * meter;
    const radCenterY = -(cfg.hubY / 2 + cfg.radPanelSpan / 2 + panelGap);
    var panelCenter = offset + vector(0 * meter, radCenterY, 0 * meter);

    buildRadPanel(context, id + "rad", panelCenter,
                  vector(0, 0, 1), cfg.radPanelChord, cfg.radPanelSpan,
                  "Radiator Panel");

    // Heat pipe texture
    addRadiatorPipeLines(context, id + "rpt", panelCenter,
                         cfg.radPanelChord, cfg.radPanelSpan, "Z", 8);

    // Radiator panel stiffening — longerons along span axis
    buildRadiatorStiffening(context, id + "rs", panelCenter,
                            cfg.radPanelChord, cfg.radPanelSpan, "Z");

    // Deployment hinge at panel root
    if (showMech)
    {
        buildHinge(context, id + "hg0",
                   offset + vector(0 * meter, -(cfg.hubY / 2 + panelGap / 2), 0 * meter),
                   vector(1, 0, 0));
    }

    // ORUs on +Z face (deep space side) — compute heat → panel → radiate to 3K
    if (showORUs && !cfg.centralized && cfg.oruCount > 0)
    {
        buildORUsOnPanel(context, id + "op0", panelCenter,
                         cfg.radPanelChord, cfg.radPanelSpan, "Z",
                         cfg.oruW, cfg.oruH, cfg.oruD,
                         cfg.oruCols, cfg.oruRows,
                         cfg.oruColor, cfg.oruColor2, cfg.oruColor3, 1);
    }

    // --- Structural truss (SpaceX: hub IS the bus, minimal added structure) ---
    if (showTruss)
    {
        // PV wing spar — the only structural extension needed
        // SpaceX: removed separate truss and cross-bar (hub provides attachment)
        fCuboid(context, id + "wspar", {
                "corner1" : offset + vector(-cfg.pvSpan, pvYCenter - TRUSS_W / 2, -TRUSS_W / 2),
                "corner2" : offset + vector( cfg.pvSpan, pvYCenter + TRUSS_W / 2,  TRUSS_W / 2)
        });
        setProperty(context, { "entities" : qCreatedBy(id + "wspar", EntityType.BODY),
                "propertyType" : PropertyType.APPEARANCE, "value" : CLR_TRUSS });
        setProperty(context, { "entities" : qCreatedBy(id + "wspar", EntityType.BODY),
                "propertyType" : PropertyType.NAME, "value" : "Wing Spar" });
    }

    // --- Bus bar ---
    if (showBusBar)
    {
        fCuboid(context, id + "bb", {
                "corner1" : offset + vector(-BB_W / 2, -cfg.hubY / 2 - BB_H, -cfg.hubZ / 2),
                "corner2" : offset + vector( BB_W / 2, -cfg.hubY / 2, cfg.hubZ / 2)
        });
        setProperty(context, { "entities" : qCreatedBy(id + "bb", EntityType.BODY),
                "propertyType" : PropertyType.APPEARANCE, "value" : CLR_GOLD });
        setProperty(context, { "entities" : qCreatedBy(id + "bb", EntityType.BODY),
                "propertyType" : PropertyType.NAME, "value" : "Bus Bar" });
    }

    // --- Mechanical detail ---
    // SpaceX: removed cable trays (cables route inside hub) and thermal straps
    // (ORU-on-radiator IS the thermal path — no separate strap needed)
    if (showMech)
    {
        // 4 RCS pods at hub corners (needed for attitude control)
        buildRCSPod(context, id + "rc0",
                    offset + vector(cfg.hubX / 2, cfg.hubY / 2, 0 * meter),
                    vector(1, 1, 0));
        buildRCSPod(context, id + "rc1",
                    offset + vector(-cfg.hubX / 2, cfg.hubY / 2, 0 * meter),
                    vector(-1, 1, 0));
        buildRCSPod(context, id + "rc2",
                    offset + vector(cfg.hubX / 2, -cfg.hubY / 2, 0 * meter),
                    vector(1, -1, 0));
        buildRCSPod(context, id + "rc3",
                    offset + vector(-cfg.hubX / 2, -cfg.hubY / 2, 0 * meter),
                    vector(-1, -1, 0));

        // Laser comm terminals
        if (cfg.isCenter)
        {
            for (var lt = 0; lt < 6; lt += 1)
            {
                var ltA = 60.0 * lt;
                var ltX = (cfg.hubX / 2 + 200 * millimeter) * cos(ltA * degree);
                var ltZ = (cfg.hubZ / 2 + 200 * millimeter) * sin(ltA * degree);
                buildLaserTerminal(context, id + ("lt" ~ lt),
                                   offset + vector(ltX, 0 * meter, ltZ),
                                   vector(0, 0, 1));
            }
            fCylinder(context, id + "ant", {
                    "bottomCenter" : offset + vector(0 * meter, 0 * meter, -cfg.hubZ / 2),
                    "topCenter" : offset + vector(0 * meter, 0 * meter, -cfg.hubZ / 2 - ANTENNA_DEPTH),
                    "radius" : ANTENNA_R
            });
            setProperty(context, { "entities" : qCreatedBy(id + "ant", EntityType.BODY),
                    "propertyType" : PropertyType.APPEARANCE, "value" : CLR_ANTENNA });
            setProperty(context, { "entities" : qCreatedBy(id + "ant", EntityType.BODY),
                    "propertyType" : PropertyType.NAME, "value" : "Ground Antenna" });
        }
        else
        {
            buildLaserTerminal(context, id + "lt0",
                               offset + vector(cfg.hubX / 2, 0 * meter, cfg.hubZ / 2),
                               vector(1, 0, 1));
            buildLaserTerminal(context, id + "lt1",
                               offset + vector(-cfg.hubX / 2, 0 * meter, cfg.hubZ / 2),
                               vector(-1, 0, 1));
        }
    }

    // --- Validation ---
    println("--- " ~ cfg.name ~ " ---");
    println("PV: " ~ (2.0 * cfg.pvChord * cfg.pvSpan / (meter * meter)) ~ " m^2");
    println("Rad: " ~ (cfg.radPanelChord * cfg.radPanelSpan / (meter * meter)) ~ " m^2");
}

// =====================================================================
// BUILD DP3 NODE
// Spine + distributed radiator branches + cryo (photonic only)
// Layout along Y: [PV at +Y] → [compute] → [cryo] → [radiator branches]
// =====================================================================
function buildNodeDP3(context is Context, id is Id, cfg is map,
                      offset is Vector, showORUs is boolean,
                      showTruss is boolean, showBusBar is boolean,
                      showMech is boolean, showCryo is boolean)
{
    // --- Hub color for center node ---
    var spineColor = CLR_TRUSS;
    if (cfg.isCenter)
    {
        spineColor = CLR_CENTER;
    }

    // --- Layout calculations (positions before geometry) ---
    // Two fundamentally different architectures:
    //
    // PHOTONIC (JWST-style): SUN → PV → RADIATOR(middle) → CRYOCOOLER → 4K COLD → CRYO RAD → DEEP SPACE
    //   Vacuum benefits photonic chips: no moisture, no scattering, thermal stability
    //   SNSPDs at 2-4K need superconducting films (require vacuum, no oxidation)
    //   Cold section at -Y gets maximum thermal isolation from sun
    //   Main radiator in middle — warm compute ORUs
    //
    // ELECTRICAL (DP2-style): SUN → PV → SPINE → RADIATOR(-Y) → DEEP SPACE
    //   Same as DP2, just scaled up. Radiator at cold end.
    //
    const halfSpine = cfg.spineLen / 2;

    // Positions depend on architecture type
    var radY = 0 * meter;       // main radiator Y position
    var cryoStartY = 0 * meter; // cryo section start
    var coldY = 0 * meter;      // 4K cold section center
    var cryoRadY = 0 * meter;   // cryo radiator (77K) at very bottom
    var spineBottom = 0 * meter;

    if (cfg.isPhotonic)
    {
        // Photonic: radiator in MIDDLE, cold section at -Y END
        // Radiator Y extent is ~11.5m (centered at radY), so clear PV by 4m
        radY = halfSpine - 12 * meter;           // main radiator below PV
        cryoStartY = radY - 8 * meter;           // cryocooler below radiator
        coldY = cryoStartY - 10 * meter;         // 4K section below cryocooler
        cryoRadY = coldY - 6 * meter;            // 77K cryo radiator at bottom
        spineBottom = cryoRadY - 2 * meter;
    }
    else
    {
        // Electrical: DP2-style, radiator at -Y end
        radY = -halfSpine + 5 * meter;
        spineBottom = radY - 1 * meter;
    }

    // --- Spine truss along Y axis ---
    // Spine spans from PV (+halfSpine) to just past radiator cross
    // SpaceX: no empty structure — spine ends where components end
    fCuboid(context, id + "spine", {
            "corner1" : offset + vector(-SPINE_W / 2, spineBottom, -SPINE_W / 2),
            "corner2" : offset + vector( SPINE_W / 2,  halfSpine,  SPINE_W / 2)
    });
    setProperty(context, { "entities" : qCreatedBy(id + "spine", EntityType.BODY),
            "propertyType" : PropertyType.APPEARANCE, "value" : spineColor });
    setProperty(context, { "entities" : qCreatedBy(id + "spine", EntityType.BODY),
            "propertyType" : PropertyType.NAME, "value" : cfg.name ~ " Spine" });

    // --- Spine cross-bracing (X-pattern diagonals) ---
    // Prevents torsional and lateral bending modes on long spines
    // 5m segments for DP3 photonic (60m spine), 5m for DP3B (30m spine)
    buildSpineBracing(context, id + "sbr", offset, spineBottom, halfSpine, 5.0);

    // --- PV arrays at sun-facing end (+Y), extending ±X ---
    // Dual-function: blue PV cells on sun side, white radiator on anti-sun side
    const pvY = halfSpine;
    const pvOff = SPINE_W / 2;
    const pvHalfT = PANEL_THICK / 2;

    // PV +X wing — sun-facing layer (blue PV cells)
    fCuboid(context, id + "pvPF", {
            "corner1" : offset + vector(pvOff, pvY, -cfg.pvChord / 2),
            "corner2" : offset + vector(pvOff + cfg.pvSpan, pvY + pvHalfT, cfg.pvChord / 2)
    });
    setProperty(context, { "entities" : qCreatedBy(id + "pvPF", EntityType.BODY),
            "propertyType" : PropertyType.APPEARANCE, "value" : CLR_PV });
    setProperty(context, { "entities" : qCreatedBy(id + "pvPF", EntityType.BODY),
            "propertyType" : PropertyType.NAME, "value" : "PV Array +X (cells)" });

    // PV +X wing — anti-sun layer (white radiator back)
    fCuboid(context, id + "pvPB", {
            "corner1" : offset + vector(pvOff, pvY - pvHalfT, -cfg.pvChord / 2),
            "corner2" : offset + vector(pvOff + cfg.pvSpan, pvY, cfg.pvChord / 2)
    });
    setProperty(context, { "entities" : qCreatedBy(id + "pvPB", EntityType.BODY),
            "propertyType" : PropertyType.APPEARANCE, "value" : CLR_RAD });
    setProperty(context, { "entities" : qCreatedBy(id + "pvPB", EntityType.BODY),
            "propertyType" : PropertyType.NAME, "value" : "PV Array +X (radiator back)" });

    // PV -X wing — sun-facing layer (blue PV cells)
    fCuboid(context, id + "pvNF", {
            "corner1" : offset + vector(-pvOff - cfg.pvSpan, pvY, -cfg.pvChord / 2),
            "corner2" : offset + vector(-pvOff, pvY + pvHalfT, cfg.pvChord / 2)
    });
    setProperty(context, { "entities" : qCreatedBy(id + "pvNF", EntityType.BODY),
            "propertyType" : PropertyType.APPEARANCE, "value" : CLR_PV });
    setProperty(context, { "entities" : qCreatedBy(id + "pvNF", EntityType.BODY),
            "propertyType" : PropertyType.NAME, "value" : "PV Array -X (cells)" });

    // PV -X wing — anti-sun layer (white radiator back)
    fCuboid(context, id + "pvNB", {
            "corner1" : offset + vector(-pvOff - cfg.pvSpan, pvY - pvHalfT, -cfg.pvChord / 2),
            "corner2" : offset + vector(-pvOff, pvY, cfg.pvChord / 2)
    });
    setProperty(context, { "entities" : qCreatedBy(id + "pvNB", EntityType.BODY),
            "propertyType" : PropertyType.APPEARANCE, "value" : CLR_RAD });
    setProperty(context, { "entities" : qCreatedBy(id + "pvNB", EntityType.BODY),
            "propertyType" : PropertyType.NAME, "value" : "PV Array -X (radiator back)" });

    // --- PV wing structural stiffening ---
    // DP3 wings cantilevered from spine — longerons + battens prevent first-mode flutter
    var pvWingRootP = offset + vector(pvOff, pvY, 0 * meter);
    buildWingStructure(context, id + "wsP", pvWingRootP, cfg.pvSpan, cfg.pvChord,
                       vector(1, 0, 0), vector(0, 0, 1), vector(0, 1, 0), showMech);
    var pvWingRootN = offset + vector(-pvOff, pvY, 0 * meter);
    buildWingStructure(context, id + "wsN", pvWingRootN, cfg.pvSpan, cfg.pvChord,
                       vector(-1, 0, 0), vector(0, 0, 1), vector(0, 1, 0), showMech);

    // --- PV panel seam lines (ISRA assembly joints) ---
    // Standard PV tile: ~4m span × 8m chord, robotically deployed and connected
    // Seam lines show modular assembly on sun-facing surface
    const pvTileSpan = 4 * meter;
    const pvTileChord = 8 * meter;
    const seamW = 30 * millimeter;  // visible seam width
    const seamH = 15 * millimeter;  // raised above PV surface
    const seamColor = color(0.02, 0.02, 0.35);  // dark blue seams

    // Span-wise seams (along X) on +X wing
    var seamIdx = 0;
    var sx = pvOff + pvTileSpan;
    while (sx < pvOff + cfg.pvSpan)
    {
        fCuboid(context, id + ("pvSX" ~ seamIdx), {
                "corner1" : offset + vector(sx - seamW / 2, pvY + pvHalfT, -cfg.pvChord / 2),
                "corner2" : offset + vector(sx + seamW / 2, pvY + pvHalfT + seamH, cfg.pvChord / 2)
        });
        setProperty(context, { "entities" : qCreatedBy(id + ("pvSX" ~ seamIdx), EntityType.BODY),
                "propertyType" : PropertyType.APPEARANCE, "value" : seamColor });
        // Mirror on -X wing
        fCuboid(context, id + ("pvSN" ~ seamIdx), {
                "corner1" : offset + vector(-sx - seamW / 2, pvY + pvHalfT, -cfg.pvChord / 2),
                "corner2" : offset + vector(-sx + seamW / 2, pvY + pvHalfT + seamH, cfg.pvChord / 2)
        });
        setProperty(context, { "entities" : qCreatedBy(id + ("pvSN" ~ seamIdx), EntityType.BODY),
                "propertyType" : PropertyType.APPEARANCE, "value" : seamColor });
        sx = sx + pvTileSpan;
        seamIdx = seamIdx + 1;
    }
    // Chord-wise seams (along Z) on +X wing
    var cz = -cfg.pvChord / 2 + pvTileChord;
    var seamCIdx = 0;
    while (cz < cfg.pvChord / 2)
    {
        fCuboid(context, id + ("pvCP" ~ seamCIdx), {
                "corner1" : offset + vector(pvOff, pvY + pvHalfT, cz - seamW / 2),
                "corner2" : offset + vector(pvOff + cfg.pvSpan, pvY + pvHalfT + seamH, cz + seamW / 2)
        });
        setProperty(context, { "entities" : qCreatedBy(id + ("pvCP" ~ seamCIdx), EntityType.BODY),
                "propertyType" : PropertyType.APPEARANCE, "value" : seamColor });
        // Mirror on -X wing
        fCuboid(context, id + ("pvCN" ~ seamCIdx), {
                "corner1" : offset + vector(-pvOff - cfg.pvSpan, pvY + pvHalfT, cz - seamW / 2),
                "corner2" : offset + vector(-pvOff, pvY + pvHalfT + seamH, cz + seamW / 2)
        });
        setProperty(context, { "entities" : qCreatedBy(id + ("pvCN" ~ seamCIdx), EntityType.BODY),
                "propertyType" : PropertyType.APPEARANCE, "value" : seamColor });
        cz = cz + pvTileChord;
        seamCIdx = seamCIdx + 1;
    }

    // ================================================================
    // MAIN RADIATOR — warm compute ORUs (370K)
    //
    // PHOTONIC: YZ plane (normal ±X), along spine mid-section
    //   Perpendicular to PV wings → zero mutual VF
    //   Both ±X faces see space (not sun, not PV, not each other)
    //   Acts as thermal wall shielding cold section below from warm PV above
    //   ORUs on +X face, -X face radiates freely
    //   Panel height along Y (spine), width along Z
    //
    // ELECTRICAL: XZ plane (normal Y), at -Y end (same as DP2)
    //   Both ±Y faces see space, ORUs on -Y face
    // ================================================================

    if (cfg.isPhotonic)
    {
        // Photonic: radiator in YZ plane, centered on radY along spine
        // Aspect 2.5:1 (height along Y : width along Z)
        // 1000 m² → 50m tall × 20m wide
        var rpCenter = offset + vector(0 * meter, radY, 0 * meter);

        buildRadPanel(context, id + "rp0", rpCenter,
                      vector(1, 0, 0), cfg.branchPanelH, cfg.branchPanelW,
                      "Main Radiator (370K)");

        addRadiatorPipeLines(context, id + "rt0", rpCenter,
                             cfg.branchPanelH, cfg.branchPanelW, "X", 6);

        // Radiator stiffening
        buildRadiatorStiffening(context, id + "rs0", rpCenter,
                                cfg.branchPanelH, cfg.branchPanelW, "X");

        // ORUs on +X face (outward, deep space)
        if (showORUs && cfg.oruCount > 0)
        {
            buildORUsOnPanel(context, id + "ro0", rpCenter,
                             cfg.branchPanelH, cfg.branchPanelW, "X",
                             cfg.oruW, cfg.oruH, cfg.oruD,
                             cfg.oruCols, cfg.oruRows,
                             cfg.oruColor, cfg.oruColor2, cfg.oruColor3, 1);
        }
    }
    else
    {
        // Electrical: radiator in XZ plane at -Y end (same as DP2)
        var rpCenter = offset + vector(0 * meter, radY, 0 * meter);

        buildRadPanel(context, id + "rp0", rpCenter,
                      vector(0, 1, 0), cfg.branchPanelW, cfg.branchPanelH,
                      "Main Radiator (370K)");

        addRadiatorPipeLines(context, id + "rt0", rpCenter,
                             cfg.branchPanelW, cfg.branchPanelH, "Y", 6);

        // Radiator stiffening
        buildRadiatorStiffening(context, id + "rs0", rpCenter,
                                cfg.branchPanelW, cfg.branchPanelH, "Y");

        // ORUs on -Y face (deep space)
        if (showORUs && cfg.oruCount > 0)
        {
            buildORUsOnPanel(context, id + "ro0", rpCenter,
                             cfg.branchPanelW, cfg.branchPanelH, "Y",
                             cfg.oruW, cfg.oruH, cfg.oruD,
                             cfg.oruCols, cfg.oruRows,
                             cfg.oruColor, cfg.oruColor2, cfg.oruColor3, -1);
        }
    }

    // ================================================================
    // PHOTONIC COLD CHAIN (photonic only)
    // JWST-style: progressively colder toward -Y (deep space)
    //   1. Cryocooler machinery (300K warm side → main radiator)
    //   2. MLI-wrapped 4K cold section (photonic processors + SNSPDs)
    //   3. Small cryo radiator at 77K (intercept stage, -Y end)
    //
    // Vacuum benefits: no moisture on optics, no oxidation of
    // superconducting films, thermal stability, free-space optical
    // coupling between photonic elements
    // ================================================================
    if (cfg.isPhotonic)
    {
        // --- Cryocooler machinery ---
        // Multi-stage: 300K → 77K → 4K
        // Warm rejection (~5-10 kW) to main radiator via heat pipe
        // Total input: ~5-10 kW for ~10W of 4K cooling
        const crMachExtent = 1.5 * meter;
        fCuboid(context, id + "crMach", {
                "corner1" : offset + vector(-crMachExtent, cryoStartY - 2 * meter, -crMachExtent),
                "corner2" : offset + vector( crMachExtent, cryoStartY + 2 * meter,  crMachExtent)
        });
        setProperty(context, { "entities" : qCreatedBy(id + "crMach", EntityType.BODY),
                "propertyType" : PropertyType.APPEARANCE, "value" : CLR_PIPE });
        setProperty(context, { "entities" : qCreatedBy(id + "crMach", EntityType.BODY),
                "propertyType" : PropertyType.NAME, "value" : "Cryocooler (300K→77K→4K)" });

        // --- 4K cold section ---
        // Photonic processors + SNSPDs (superconducting nanowire detectors)
        // MLI-wrapped for thermal isolation
        // Si₃N₄ waveguides (radiation-hard) + LiNbO₃ modulators (cryo-compatible)
        if (showCryo)
        {
            const coldExtent = SPINE_W / 2 + 500 * millimeter;

            // MLI thermal blanket
            fCuboid(context, id + "crMLI", {
                    "corner1" : offset + vector(-coldExtent, coldY - 5 * meter, -coldExtent),
                    "corner2" : offset + vector( coldExtent, coldY + 5 * meter,  coldExtent)
            });
            setProperty(context, { "entities" : qCreatedBy(id + "crMLI", EntityType.BODY),
                    "propertyType" : PropertyType.APPEARANCE, "value" : CLR_MLI });
            setProperty(context, { "entities" : qCreatedBy(id + "crMLI", EntityType.BODY),
                    "propertyType" : PropertyType.NAME, "value" : "4K Cold Section (MLI)" });

            // Photonic processor modules inside cold section
            for (var ci = 0; ci < 3; ci += 1)
            {
                var cmId = "ph" ~ ci;
                var cmY = coldY - 3 * meter + ci * 3 * meter;
                fCuboid(context, id + cmId, {
                        "corner1" : offset + vector(-1 * meter, cmY - 0.5 * meter, -1 * meter),
                        "corner2" : offset + vector( 1 * meter, cmY + 0.5 * meter,  1 * meter)
                });
                setProperty(context, { "entities" : qCreatedBy(id + cmId, EntityType.BODY),
                        "propertyType" : PropertyType.APPEARANCE, "value" : CLR_CRYO });
                setProperty(context, { "entities" : qCreatedBy(id + cmId, EntityType.BODY),
                        "propertyType" : PropertyType.NAME, "value" : "Photonic Processor " ~ ci ~ " (4K)" });
            }

            // --- 77K cryo radiator at very bottom ---
            // Small panel for intermediate cryocooler stage
            // Sized for ~100W at 77K: σε(77⁴)×sidedness ≈ 30 W/m² → ~3.3 m²
            // Use 5 m² with margin → ~2.2m × 2.2m
            const cryoRadSize = 2.5 * meter;
            var cryoRpCenter = offset + vector(0 * meter, cryoRadY, 0 * meter);
            buildRadPanel(context, id + "crRad", cryoRpCenter,
                          vector(0, 1, 0), cryoRadSize, cryoRadSize,
                          "Cryo Radiator (77K)");
            setProperty(context, { "entities" : qCreatedBy(id + "crRad", EntityType.BODY),
                    "propertyType" : PropertyType.APPEARANCE, "value" : CLR_CRYO });
        }
    }

    // --- Heat pipe manifolds ---
    if (showMech)
    {
        for (var hp = 0; hp < 4; hp += 1)
        {
            var hId = "hp" ~ hp;
            var hpX = SPINE_W / 2;
            var hpZ = SPINE_W / 2;
            if (hp == 1 || hp == 3) { hpX = -hpX; }
            if (hp >= 2) { hpZ = -hpZ; }
            fCylinder(context, id + hId, {
                    "bottomCenter" : offset + vector(hpX, spineBottom, hpZ),
                    "topCenter" : offset + vector(hpX, halfSpine, hpZ),
                    "radius" : HEAT_PIPE_R
            });
            setProperty(context, { "entities" : qCreatedBy(id + hId, EntityType.BODY),
                    "propertyType" : PropertyType.APPEARANCE, "value" : CLR_PIPE });
            setProperty(context, { "entities" : qCreatedBy(id + hId, EntityType.BODY),
                    "propertyType" : PropertyType.NAME, "value" : "Heat Pipe " ~ hp });
        }

        // Docking port
        fCylinder(context, id + "dock", {
                "bottomCenter" : offset + vector(0 * meter, 0 * meter, SPINE_W / 2),
                "topCenter" : offset + vector(0 * meter, 0 * meter, SPINE_W / 2 + 500 * millimeter),
                "radius" : 400 * millimeter
        });
        setProperty(context, { "entities" : qCreatedBy(id + "dock", EntityType.BODY),
                "propertyType" : PropertyType.APPEARANCE, "value" : CLR_RCS });
        setProperty(context, { "entities" : qCreatedBy(id + "dock", EntityType.BODY),
                "propertyType" : PropertyType.NAME, "value" : "Docking Port" });

        // Laser terminals
        if (cfg.isCenter)
        {
            for (var lt = 0; lt < 6; lt += 1)
            {
                var ltA = 60.0 * lt;
                var ltX = (SPINE_W / 2 + 500 * millimeter) * cos(ltA * degree);
                var ltZ = (SPINE_W / 2 + 500 * millimeter) * sin(ltA * degree);
                buildLaserTerminal(context, id + ("lt" ~ lt),
                                   offset + vector(ltX, 0 * meter, ltZ),
                                   vector(cos(ltA * degree), 0, sin(ltA * degree)));
            }
            fCylinder(context, id + "ant", {
                    "bottomCenter" : offset + vector(0 * meter, 0 * meter, -SPINE_W / 2),
                    "topCenter" : offset + vector(0 * meter, 0 * meter, -SPINE_W / 2 - ANTENNA_DEPTH),
                    "radius" : ANTENNA_R
            });
            setProperty(context, { "entities" : qCreatedBy(id + "ant", EntityType.BODY),
                    "propertyType" : PropertyType.APPEARANCE, "value" : CLR_ANTENNA });
            setProperty(context, { "entities" : qCreatedBy(id + "ant", EntityType.BODY),
                    "propertyType" : PropertyType.NAME, "value" : "Ground Antenna" });
        }
        else
        {
            buildLaserTerminal(context, id + "lt0",
                               offset + vector(SPINE_W / 2, 0 * meter, SPINE_W / 2),
                               vector(1, 0, 1));
            buildLaserTerminal(context, id + "lt1",
                               offset + vector(-SPINE_W / 2, 0 * meter, SPINE_W / 2),
                               vector(-1, 0, 1));
        }
    }

    // --- Bus bar ---
    if (showBusBar)
    {
        fCuboid(context, id + "bb", {
                "corner1" : offset + vector(-BB_W / 2, spineBottom, SPINE_W / 2),
                "corner2" : offset + vector( BB_W / 2,  halfSpine, SPINE_W / 2 + BB_H)
        });
        setProperty(context, { "entities" : qCreatedBy(id + "bb", EntityType.BODY),
                "propertyType" : PropertyType.APPEARANCE, "value" : CLR_GOLD });
        setProperty(context, { "entities" : qCreatedBy(id + "bb", EntityType.BODY),
                "propertyType" : PropertyType.NAME, "value" : "Power Bus Bar" });
    }

    println("--- " ~ cfg.name ~ " ---");
    println("Spine: " ~ (cfg.spineLen / meter) ~ " m, Branches: " ~ cfg.branchCount);
}

// =====================================================================
// DRAGONFLY LINKS
// =====================================================================
function buildDragonflyLinks(context is Context, id is Id, positions is array)
{
    var linkIdx = 0;
    for (var i = 0; i < size(positions); i += 1)
    {
        for (var j = i + 1; j < size(positions); j += 1)
        {
            var lId = "lk" ~ linkIdx;
            fCylinder(context, id + lId, {
                    "bottomCenter" : positions[i],
                    "topCenter" : positions[j],
                    "radius" : LINK_R
            });
            setProperty(context, { "entities" : qCreatedBy(id + lId, EntityType.BODY),
                    "propertyType" : PropertyType.APPEARANCE, "value" : CLR_LINK });
            setProperty(context, { "entities" : qCreatedBy(id + lId, EntityType.BODY),
                    "propertyType" : PropertyType.NAME, "value" : "Dragonfly Link " ~ i ~ "-" ~ j });
            linkIdx += 1;
        }
    }
    println("Dragonfly links: " ~ linkIdx);
}

// =====================================================================
// MAIN FEATURE
// =====================================================================
annotation { "Feature Type Name" : "Orbital Compute Station" }
export const orbitalComputeStation = defineFeature(function(context is Context, id is Id, definition is map)
    precondition
    {
        annotation { "Name" : "Station Configuration" }
        definition.preset is StationPreset;

        annotation { "Group Name" : "Display", "Collapsed By Default" : false }
        {
            annotation { "Name" : "Show ORUs", "Default" : true }
            definition.showORUs is boolean;
            annotation { "Name" : "Show Truss/Spine", "Default" : true }
            definition.showTruss is boolean;
            annotation { "Name" : "Show Bus Bar", "Default" : true }
            definition.showBusBar is boolean;
            annotation { "Name" : "Show Dragonfly Links", "Default" : true }
            definition.showDragonfly is boolean;
            annotation { "Name" : "Show Mechanical Detail", "Default" : false }
            definition.showMech is boolean;
            annotation { "Name" : "Show Cryo Section", "Default" : true }
            definition.showCryo is boolean;
        }
    }
    {
        const p = definition.preset;
        const origin = vector(0, 0, 0) * meter;

        if (p == StationPreset.DP2 || p == StationPreset.DP2B)
        {
            const cfg = getNodeConfig(p, "compute", false);
            buildNodeDP2(context, id + "m", cfg, origin,
                         definition.showORUs, definition.showTruss,
                         definition.showBusBar, definition.showMech);
        }
        else if (p == StationPreset.DP3 || p == StationPreset.DP3B)
        {
            const cfg = getNodeConfig(p, "compute", false);
            buildNodeDP3(context, id + "m", cfg, origin,
                         definition.showORUs, definition.showTruss,
                         definition.showBusBar, definition.showMech,
                         definition.showCryo);
        }
        else if (p == StationPreset.DP2_CLUSTER)
        {
            const clusterR = 80 * meter;
            const cCfg = getNodeConfig(StationPreset.DP2_CLUSTER, "routing", true);
            buildNodeDP2(context, id + "ct", cCfg, origin,
                         definition.showORUs, definition.showTruss,
                         definition.showBusBar, definition.showMech);

            var positions = [origin];
            for (var ei = 0; ei < 4; ei += 1)
            {
                var angle = 90.0 * ei;
                var ePos = vector(clusterR * cos(angle * degree),
                                  clusterR * sin(angle * degree),
                                  0 * meter);
                positions = append(positions, ePos);

                // Edge roles: decode handles token gen (latency-critical),
                // prefill does parallel prompt processing, context stores KV cache
                var eRole = "decode";
                if (ei >= 1 && ei < 3) { eRole = "prefill"; }
                if (ei >= 3) { eRole = "context"; }
                const eCfg = getNodeConfig(StationPreset.DP2_CLUSTER, eRole, false);
                buildNodeDP2(context, id + ("e" ~ ei), eCfg, ePos,
                             definition.showORUs, definition.showTruss,
                             definition.showBusBar, definition.showMech);
            }
            if (definition.showDragonfly)
            {
                buildDragonflyLinks(context, id + "df", positions);
            }
            println("=== DP2 CLUSTER: 1 center + 4 edge, 80m ===");
        }
        else if (p == StationPreset.DP3_CLUSTER_PH)
        {
            const clusterR = 300 * meter;
            const cCfg = getNodeConfig(StationPreset.DP3_CLUSTER_PH, "routing", true);
            buildNodeDP3(context, id + "ct", cCfg, origin,
                         definition.showORUs, definition.showTruss,
                         definition.showBusBar, definition.showMech,
                         definition.showCryo);

            var positions = [origin];
            for (var ei = 0; ei < 9; ei += 1)
            {
                var angle = 40.0 * ei;
                var ePos = vector(clusterR * cos(angle * degree),
                                  clusterR * sin(angle * degree),
                                  0 * meter);
                positions = append(positions, ePos);

                // 9 edge nodes: 3 decode, 3 prefill, 3 context
                var eRole = "decode";
                if (ei >= 3 && ei < 6) { eRole = "prefill"; }
                if (ei >= 6) { eRole = "context"; }
                const eCfg = getNodeConfig(StationPreset.DP3_CLUSTER_PH, eRole, false);
                buildNodeDP3(context, id + ("e" ~ ei), eCfg, ePos,
                             definition.showORUs, definition.showTruss,
                             definition.showBusBar, definition.showMech,
                             definition.showCryo);
            }
            if (definition.showDragonfly)
            {
                buildDragonflyLinks(context, id + "df", positions);
            }
            println("=== DP3 CLUSTER PH: 1 center + 9 edge, 300m ===");
        }
        else if (p == StationPreset.DP3_CLUSTER_EL)
        {
            const clusterR = 300 * meter;
            const cCfg = getNodeConfig(StationPreset.DP3_CLUSTER_EL, "routing", true);
            buildNodeDP3(context, id + "ct", cCfg, origin,
                         definition.showORUs, definition.showTruss,
                         definition.showBusBar, definition.showMech,
                         definition.showCryo);

            var positions = [origin];
            for (var ei = 0; ei < 9; ei += 1)
            {
                var angle = 40.0 * ei;
                var ePos = vector(clusterR * cos(angle * degree),
                                  clusterR * sin(angle * degree),
                                  0 * meter);
                positions = append(positions, ePos);

                var eRole = "decode";
                if (ei >= 3 && ei < 6) { eRole = "prefill"; }
                if (ei >= 6) { eRole = "context"; }
                const eCfg = getNodeConfig(StationPreset.DP3_CLUSTER_EL, eRole, false);
                buildNodeDP3(context, id + ("e" ~ ei), eCfg, ePos,
                             definition.showORUs, definition.showTruss,
                             definition.showBusBar, definition.showMech,
                             definition.showCryo);
            }
            if (definition.showDragonfly)
            {
                buildDragonflyLinks(context, id + "df", positions);
            }
            println("=== DP3 CLUSTER EL: 1 center + 9 edge, 300m ===");
        }
    });
