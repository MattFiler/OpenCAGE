<?php
// api.php

// Include database credentials
if (file_exists('keys.php')) {
    require_once 'keys.php';
} else {
    http_response_code(500);
    echo json_encode(['error' => 'Database keys file not found. Please create keys.php']);
    exit;
}


header("Content-Type: application/json");
header("Access-Control-Allow-Origin: *"); // For development; restrict in production

// --- Database Configuration ---
define('DB_HOST', 'localhost');
define('DB_NAME', 'OpenCAGE'); 

define('DB_USER', isset($username) ? $username : 'default_user_fallback');
define('DB_PASSWORD', isset($password) ? $password : 'default_password_fallback');


// --- PDO Database Connection ---
try {
    $pdo = new PDO("mysql:host=" . DB_HOST . ";dbname=" . DB_NAME . ";charset=utf8mb4", DB_USER, DB_PASSWORD);
    $pdo->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);
    $pdo->setAttribute(PDO::ATTR_DEFAULT_FETCH_MODE, PDO::FETCH_ASSOC);
} catch (PDOException $e) {
    http_response_code(500);
    echo json_encode(['error' => 'Database connection failed: ' . $e->getMessage() . (isset($username) ? '' : ' (DB username not set from keys.php)')]);
    exit;
}

// --- Helper Function: Parse date strings ---
function parseReleaseDate($dateStr) {
    if (empty(trim($dateStr))) return null;
    try {
        return (new DateTime($dateStr))->format('Y-m-d');
    } catch (Exception $e) { return null; }
}

// --- Helper Function: Get All Version Details (Sorted Oldest to Newest) ---
function getAllVersionDetails($pdo) {
    $versions = [];
    $stmt = $pdo->query("SELECT version_number, release_date FROM versions WHERE release_date IS NOT NULL AND TRIM(release_date) != ''");
    $tempVersions = [];
    while ($row = $stmt->fetch()) {
        $parsedDate = parseReleaseDate($row['release_date']);
        if ($parsedDate) {
            $tempVersions[] = ['version_number' => $row['version_number'], 'original_release_date' => $row['release_date'], 'parsed_release_date' => $parsedDate];
        }
    }
    // Also fetch versions without dates, just store null
    $stmtNoDate = $pdo->query("SELECT version_number FROM versions WHERE release_date IS NULL OR TRIM(release_date) = ''");
    while ($row = $stmtNoDate->fetch()) {
         // Avoid adding if already added with a date (shouldn't happen with UNIQUE constraint)
         $already_added = false;
         foreach($tempVersions as $v) {
             if ($v['version_number'] === $row['version_number']) {
                 $already_added = true;
                 break;
             }
         }
         if (!$already_added) {
             $tempVersions[] = [
                 'version_number' => $row['version_number'],
                 'original_release_date' => null,
                 'parsed_release_date' => null
             ];
         }
    }

    // Sort primarily by parsed_release_date (nulls might go first or last depending on DB/PHP version, handle if needed)
    // Secondary sort by version_number ensures consistency
    usort($tempVersions, function($a, $b) {
        $dateComparison = 0;
        if ($a['parsed_release_date'] && $b['parsed_release_date']) {
             $dateComparison = strcmp($a['parsed_release_date'], $b['parsed_release_date']);
        } elseif ($a['parsed_release_date']) {
            $dateComparison = 1; // a has date, b doesn't, a is "greater"
        } elseif ($b['parsed_release_date']) {
            $dateComparison = -1; // b has date, a doesn't, b is "greater"
        }
        // If dates are the same or both null, sort by version
        return ($dateComparison === 0) ? version_compare($a['version_number'], $b['version_number']) : $dateComparison;
    });

    foreach($tempVersions as $vData) {
        // Use original_release_date for potential display/hover info
        $versions[$vData['version_number']] = [
            'original_release_date' => $vData['original_release_date'], 
            'parsed_release_date' => $vData['parsed_release_date']
        ];
    }
    return $versions; // Map of version_number => details
}

// --- Helper Function: Get Sorted Dated Versions Map (Oldest to Newest) ---
function getSortedDatedVersionNumbersAndDates($allVersionDetails) {
    $sortableVersions = [];
    foreach ($allVersionDetails as $version => $details) {
        // Only include versions that HAVE a parsed date for this specific map
        if(!empty($details['parsed_release_date'])) {
             $sortableVersions[] = ['version' => $version, 'date' => $details['parsed_release_date']];
        }
    }
    usort($sortableVersions, function($a, $b) {
        $dateComparison = strcmp($a['date'], $b['date']);
        return ($dateComparison === 0) ? version_compare($a['version'], $b['version']) : $dateComparison;
    });
    $sortedMap = [];
    foreach ($sortableVersions as $item) $sortedMap[$item['version']] = $item['date'];
    return $sortedMap;
}

// --- Helper Function: Determine Grouped Version ---
function getGroupedVersion($analyticsVersion, $allVersionDetails, $sortedDatedVersionNumbersOnly) {
    // Check if the version itself is in the details map AND has a parsed date
    if (isset($allVersionDetails[$analyticsVersion]) && !empty($allVersionDetails[$analyticsVersion]['parsed_release_date'])) {
        return $analyticsVersion;
    }
    $groupedVersion = $analyticsVersion; // Default if no group found
    // Find the latest DATED version <= the analytics version
    for ($i = count($sortedDatedVersionNumbersOnly) - 1; $i >= 0; $i--) {
        $datedVersionCandidate = $sortedDatedVersionNumbersOnly[$i];
        if (version_compare($datedVersionCandidate, $analyticsVersion, '<=')) {
            $groupedVersion = $datedVersionCandidate; 
            break;
        }
    } 
    return $groupedVersion;
}

// --- Helper Function: Calculate Linear Regression ---
function calculateLinearRegression($x_values, $y_values) {
    $n = count($x_values);
    if ($n <= 1 || $n !== count($y_values)) return [0, 0]; // Need at least 2 points

    $sum_x = array_sum($x_values);
    $sum_y = array_sum($y_values);
    $sum_xy = 0;
    $sum_x_sq = 0;

    for ($i = 0; $i < $n; $i++) {
        $sum_xy += $x_values[$i] * $y_values[$i];
        $sum_x_sq += $x_values[$i] * $x_values[$i];
    }

    $denominator = ($n * $sum_x_sq) - ($sum_x * $sum_x);
    if (abs($denominator) < 1e-9) return [0, $sum_y / $n]; // Avoid division by zero (vertical line or single point x value)

    $slope = (($n * $sum_xy) - ($sum_x * $sum_y)) / $denominator;
    $intercept = ($sum_y - ($slope * $sum_x)) / $n;

    return [$slope, $intercept];
}


// --- API Action Routing ---
$action = $_GET['action'] ?? '';
switch ($action) {
    case 'getOverallStats': getOverallStats($pdo); break;
    case 'getStatsPerVersion': getStatsPerVersion($pdo); break;
    case 'getUserHistory':
        $clientId = $_GET['clientId'] ?? null;
        if ($clientId) getUserHistory($pdo, $clientId); // Modified below
        else { http_response_code(400); echo json_encode(['error' => 'Client ID is required.']); }
        break;
    case 'getAllClientIds': getAllClientIds($pdo); break; 
    case 'getReleaseTimeline': getReleaseTimeline($pdo); break;
    case 'getPopularityData': getPopularityData($pdo); break;
    case 'getReleaseHeatmapData': getReleaseHeatmapData($pdo); break;
    case 'getTopUsers': getTopUsers($pdo); break;
    case 'getLatestVersionStats': getLatestVersionStats($pdo); break; 
    default: http_response_code(404); echo json_encode(['error' => 'Action not found.']); break;
}

// --- API Functions ---
function getOverallStats($pdo) { /* ... (no changes) ... */ 
    $totalUniqueUsers = $pdo->query("SELECT COUNT(DISTINCT client_id) FROM analytics")->fetchColumn();
    $totalSessions = $pdo->query("SELECT SUM(launch_count) FROM analytics")->fetchColumn();
    $totalVersions = $pdo->query("SELECT COUNT(id) FROM versions WHERE release_date IS NOT NULL AND TRIM(release_date) != ''")->fetchColumn();
    echo json_encode([
        'totalUniqueUsers' => (int)$totalUniqueUsers,
        'totalSessions' => (int)$totalSessions,
        'totalVersions' => (int)$totalVersions
    ]);
}
function getAllClientIds($pdo) { /* ... (no changes) ... */ 
    $stmt = $pdo->query("
        SELECT client_id, SUM(launch_count) as total_sessions
        FROM analytics
        GROUP BY client_id
        ORDER BY total_sessions DESC
    ");
    $result = $stmt->fetchAll(PDO::FETCH_ASSOC); 
    echo json_encode($result);
}
function getStatsPerVersion($pdo) { /* ... (no changes) ... */ 
    $allVersionDetails = getAllVersionDetails($pdo);
    $sortedDatedVersionsMap = getSortedDatedVersionNumbersAndDates($allVersionDetails);
    $sortedDatedVersionNumbersOnly = array_keys($sortedDatedVersionsMap);

    $stmt = $pdo->query("SELECT client_id, version_number, launch_count FROM analytics");
    $analyticsData = $stmt->fetchAll();
    $groupedStats = [];

    foreach ($analyticsData as $row) {
        $originalVersion = $row['version_number'];
        $launchCount = (int)$row['launch_count'];
        $clientId = $row['client_id'];
        $groupedVersionKey = getGroupedVersion($originalVersion, $allVersionDetails, $sortedDatedVersionNumbersOnly);

        if (!isset($allVersionDetails[$groupedVersionKey])) continue; // Only include stats for versions that group to a dated version

        if (!isset($groupedStats[$groupedVersionKey])) {
            $groupedStats[$groupedVersionKey] = ['version' => $groupedVersionKey, 'totalSessions' => 0, 'uniqueClientsMap' => []];
        }
        $groupedStats[$groupedVersionKey]['totalSessions'] += $launchCount;
        $groupedStats[$groupedVersionKey]['uniqueClientsMap'][$clientId] = true;
    }

    $result = [];
    $totalOverallSessions = 0;
    $totalOverallUniqueUsers = 0;
    $versionCountForAverage = 0;

    foreach ($groupedStats as $versionData) {
        $uniqueUserCount = count($versionData['uniqueClientsMap']);
        $result[] = ['version' => $versionData['version'], 'totalSessions' => $versionData['totalSessions'], 'uniqueUsers' => $uniqueUserCount];
        $totalOverallSessions += $versionData['totalSessions'];
        $totalOverallUniqueUsers += $uniqueUserCount;
        $versionCountForAverage++;
    }
    usort($result, function($a, $b) { return version_compare($b['version'], $a['version']); });

    echo json_encode([
        'stats' => $result,
        'averageSessions' => $versionCountForAverage > 0 ? round($totalOverallSessions / $versionCountForAverage, 2) : 0,
        'averageUniqueUsers' => $versionCountForAverage > 0 ? round($totalOverallUniqueUsers / $versionCountForAverage, 2) : 0
    ]);
}

// *** MODIFIED FUNCTION: getUserHistory ***
function getUserHistory($pdo, $clientId) {
    // Get all version details (including those without dates) once
    $allVersionDetails = getAllVersionDetails($pdo); 

    // Fetch raw analytics data for the specific client
    $stmt = $pdo->prepare("SELECT version_number, launch_count FROM analytics WHERE client_id = :client_id");
    $stmt->execute(['client_id' => $clientId]);
    $userAnalytics = $stmt->fetchAll();

    $userHistoryResult = [];

    foreach ($userAnalytics as $row) {
        $versionNumber = $row['version_number'];
        $sessions = (int)$row['launch_count'];
        
        // Look up the release date for this specific version
        $releaseDate = null;
        if (isset($allVersionDetails[$versionNumber]) && !empty($allVersionDetails[$versionNumber]['original_release_date'])) {
            $releaseDate = $allVersionDetails[$versionNumber]['original_release_date'];
        }

        // Add to result array
        $userHistoryResult[] = [
            'version' => $versionNumber, // The actual version used
            'sessions' => $sessions,
            'releaseDate' => $releaseDate // Original date string or null
        ];
    }
    
    // Sort the final result by version number descending for display
    usort($userHistoryResult, function($a, $b) { 
        return version_compare($b['version'], $a['version']); 
    });

    echo json_encode($userHistoryResult);
}

function getReleaseTimeline($pdo) { /* ... (no changes) ... */ 
    $allVersionDetails = getAllVersionDetails($pdo);
    $sortedDatedVersionsMap = getSortedDatedVersionNumbersAndDates($allVersionDetails); 
    
    $statsPerVersionForTimeline = [];
    $stmtAnalytics = $pdo->query("SELECT client_id, version_number, launch_count FROM analytics");
    $analyticsData = $stmtAnalytics->fetchAll();
    $tempGroupedStats = [];
    $sortedDatedVersionNumbersOnly = array_keys($sortedDatedVersionsMap);

    foreach ($analyticsData as $row) {
        $originalVersion = $row['version_number'];
        $launchCount = (int)$row['launch_count'];
        $clientId = $row['client_id'];
        $groupedVersionKey = getGroupedVersion($originalVersion, $allVersionDetails, $sortedDatedVersionNumbersOnly);
        if (!isset($allVersionDetails[$groupedVersionKey])) continue;
        if (!isset($tempGroupedStats[$groupedVersionKey])) {
            $tempGroupedStats[$groupedVersionKey] = ['totalSessions' => 0, 'uniqueClientsMap' => []];
        }
        $tempGroupedStats[$groupedVersionKey]['totalSessions'] += $launchCount;
        $tempGroupedStats[$groupedVersionKey]['uniqueClientsMap'][$clientId] = true;
    }
    foreach ($tempGroupedStats as $version => $data) {
        $statsPerVersionForTimeline[$version] = ['sessions' => $data['totalSessions'], 'users' => count($data['uniqueClientsMap'])];
    }

    $timelineData = [];
    $versionKeysSortedByDate = array_keys($sortedDatedVersionsMap);
    $today = new DateTime();

    for ($i = 0; $i < count($versionKeysSortedByDate); $i++) {
        $version = $versionKeysSortedByDate[$i];
        $parsedDate = $sortedDatedVersionsMap[$version];
        $releaseDateObj = new DateTime($parsedDate);

        $daysSincePreviousRelease = null;
        if ($i > 0) {
            $prevVersionReleaseDateObj = new DateTime($sortedDatedVersionsMap[$versionKeysSortedByDate[$i-1]]);
            $daysSincePreviousRelease = $releaseDateObj->diff($prevVersionReleaseDateObj)->days;
        }

        $daysAvailable = ($i + 1 < count($versionKeysSortedByDate))
            ? (new DateTime($sortedDatedVersionsMap[$versionKeysSortedByDate[$i+1]]))->diff($releaseDateObj)->days
            : $today->diff($releaseDateObj)->days;
        $daysAvailable = max(1, $daysAvailable);


        $timelineData[] = [
            'version' => $version,
            'releaseDate' => $parsedDate, 
            'originalReleaseDate' => $allVersionDetails[$version]['original_release_date'],
            'sessions' => $statsPerVersionForTimeline[$version]['sessions'] ?? 0,
            'users' => $statsPerVersionForTimeline[$version]['users'] ?? 0,
            'daysSincePreviousRelease' => $daysSincePreviousRelease,
            'daysAvailable' => $daysAvailable
        ];
    }
    echo json_encode($timelineData);
}
function getPopularityData($pdo) { /* ... (no changes) ... */ 
    $allVersionDetails = getAllVersionDetails($pdo);
    $sortedDatedVersionsMap = getSortedDatedVersionNumbersAndDates($allVersionDetails); 
    $sortedDatedVersionNumbersOnly = array_keys($sortedDatedVersionsMap);

    $sessionsPerGroupedVersion = [];
    $uniqueUsersPerGroupedVersion = [];
    
    $stmtAnalytics = $pdo->query("SELECT client_id, version_number, launch_count FROM analytics");
    $analyticsData = $stmtAnalytics->fetchAll();

    foreach ($analyticsData as $row) {
        $originalVersion = $row['version_number'];
        $clientId = $row['client_id'];
        $launchCount = (int)$row['launch_count'];
        $groupedVersionKey = getGroupedVersion($originalVersion, $allVersionDetails, $sortedDatedVersionNumbersOnly);

        if (!isset($allVersionDetails[$groupedVersionKey])) continue;

        if (!isset($sessionsPerGroupedVersion[$groupedVersionKey])) $sessionsPerGroupedVersion[$groupedVersionKey] = 0;
        $sessionsPerGroupedVersion[$groupedVersionKey] += $launchCount;
        
        if (!isset($uniqueUsersPerGroupedVersion[$groupedVersionKey])) $uniqueUsersPerGroupedVersion[$groupedVersionKey] = [];
        $uniqueUsersPerGroupedVersion[$groupedVersionKey][$clientId] = true;
    }

    $popularityResult = [];
    $today = new DateTime();
    $versionKeysSortedByDate = array_keys($sortedDatedVersionsMap);

    $x_values_for_trend = []; 
    $y_values_for_trend = []; 

    for ($i = 0; $i < count($versionKeysSortedByDate); $i++) {
        $version = $versionKeysSortedByDate[$i];
        $details = $allVersionDetails[$version];
        $releaseDateObj = new DateTime($details['parsed_release_date']);
        
        $daysSincePreviousRelease = null;
        if ($i > 0) {
            $prevVersionReleaseDateObj = new DateTime($sortedDatedVersionsMap[$versionKeysSortedByDate[$i-1]]);
            $daysSincePreviousRelease = $releaseDateObj->diff($prevVersionReleaseDateObj)->days;
        }

        $daysAvailable = ($i + 1 < count($versionKeysSortedByDate))
            ? (new DateTime($sortedDatedVersionsMap[$versionKeysSortedByDate[$i+1]]))->diff($releaseDateObj)->days
            : $today->diff($releaseDateObj)->days; 
        $daysAvailable = max(1, $daysAvailable);

        $uniqueUserCount = isset($uniqueUsersPerGroupedVersion[$version]) ? count($uniqueUsersPerGroupedVersion[$version]) : 0;
        $totalSessionsForVersion = $sessionsPerGroupedVersion[$version] ?? 0;
        
        $sessionsPerUser = ($uniqueUserCount > 0) ? ($totalSessionsForVersion / $uniqueUserCount) : 0;
        $engagementFactor = log10($sessionsPerUser + 1); 
        
        $daysFactor = log10($daysAvailable + 1); 
        if ($daysFactor <= 0) $daysFactor = log10(2); 
        
        $basePopularity = ($uniqueUserCount > 0) ? ($uniqueUserCount / $daysFactor) : 0;
        $popularityScore = $basePopularity * (1 + $engagementFactor);
        
        $releaseTimestamp = $releaseDateObj->getTimestamp();

        $popularityResult[] = [
            'version' => $version,
            'releaseDate' => $details['parsed_release_date'], 
            'originalReleaseDate' => $details['original_release_date'],
            'totalUsers' => $uniqueUserCount, 
            'totalSessions' => $totalSessionsForVersion, 
            'popularityScore' => round($popularityScore, 3), 
            'daysSincePreviousRelease' => $daysSincePreviousRelease, 
            'daysAvailable' => $daysAvailable,
            'releaseTimestamp' => $releaseTimestamp 
        ];
        
        $x_values_for_trend[] = $releaseTimestamp;
        $y_values_for_trend[] = $popularityScore;
    }
    
    list($trendSlope, $trendIntercept) = calculateLinearRegression($x_values_for_trend, $y_values_for_trend);

    echo json_encode([
        'data' => $popularityResult,
        'trendline' => [
            'slope' => $trendSlope,
            'intercept' => $trendIntercept
        ]
    ]);
}
function getReleaseHeatmapData($pdo) { /* ... (no changes) ... */ 
    $allVersionDetails = getAllVersionDetails($pdo);
    if (empty($allVersionDetails)) { echo json_encode([]); return; }

    $releaseDatesOnly = [];
    foreach($allVersionDetails as $details) $releaseDatesOnly[] = $details['parsed_release_date'];
    sort($releaseDatesOnly); 
    if (empty($releaseDatesOnly)) { echo json_encode([]); return; }

    $firstReleaseDate = new DateTime(reset($releaseDatesOnly));
    $heatmapEndDate = new DateTime(max(end($releaseDatesOnly), (new DateTime('now'))->format('Y-m-d'))); 
    $heatmapEndDate->modify('last day of this month');

    $heatmapData = [];
    $period = new DatePeriod($firstReleaseDate, new DateInterval('P1M'), $heatmapEndDate->modify('+1 day'));
    $releasesByMonth = [];
    foreach ($releaseDatesOnly as $dateStr) {
        $monthYear = (new DateTime($dateStr))->format('Y-m');
        $releasesByMonth[$monthYear] = ($releasesByMonth[$monthYear] ?? 0) + 1;
    }
    $maxReleasesInMonth = !empty($releasesByMonth) ? max($releasesByMonth) : 1;

    foreach ($period as $dt) {
        $monthYearKey = $dt->format('Y-m');
        $releaseCount = $releasesByMonth[$monthYearKey] ?? 0;
        $heatmapData[] = [
            'month' => $dt->format('M'), 
            'year' => $dt->format('Y'),  
            'isoMonthYear' => $monthYearKey,
            'releaseCount' => $releaseCount,
            'intensity' => $releaseCount / $maxReleasesInMonth
        ];
    }
    echo json_encode($heatmapData);
}
function getTopUsers($pdo) { /* ... (no changes) ... */ 
    $allVersionDetails = getAllVersionDetails($pdo); 
    $sortedDatedVersionsMap = getSortedDatedVersionNumbersAndDates($allVersionDetails);
    $sortedDatedVersionNumbersOnly = array_keys($sortedDatedVersionsMap);

    $stmtTopUsers = $pdo->query("
        SELECT client_id, SUM(launch_count) as total_sessions
        FROM analytics
        GROUP BY client_id
        ORDER BY total_sessions DESC
        LIMIT 10
    ");
    $topUsersData = $stmtTopUsers->fetchAll();

    $rankedUsers = [];
    $rank = 1;
    foreach ($topUsersData as $userData) {
        $clientId = $userData['client_id'];
        
        $stmtClientVersions = $pdo->prepare("SELECT DISTINCT version_number FROM analytics WHERE client_id = :client_id");
        $stmtClientVersions->execute(['client_id' => $clientId]);
        $rawVersionsUsed = $stmtClientVersions->fetchAll(PDO::FETCH_COLUMN);

        $groupedVersionsUsedMap = [];
        foreach($rawVersionsUsed as $rawVersion) {
            $groupedVersion = getGroupedVersion($rawVersion, $allVersionDetails, $sortedDatedVersionNumbersOnly);
            if (isset($allVersionDetails[$groupedVersion])) { 
                 $groupedVersionsUsedMap[$groupedVersion] = true;
            }
        }
        $groupedVersionsList = array_keys($groupedVersionsUsedMap);
        usort($groupedVersionsList, 'version_compare'); 

        $rankedUsers[] = [
            'rank' => $rank++,
            'clientId' => $clientId,
            'totalSessions' => (int)$userData['total_sessions'],
            'versionsUsedCount' => count($groupedVersionsList),
            'versionsUsedList' => $groupedVersionsList 
        ];
    }
    echo json_encode($rankedUsers);
}
function getLatestVersionStats($pdo) { /* ... (no changes) ... */ 
    $allVersionDetails = getAllVersionDetails($pdo);
    $sortedDatedVersionsMap = getSortedDatedVersionNumbersAndDates($allVersionDetails);

    if (empty($sortedDatedVersionsMap)) {
        echo json_encode(['versionNumber' => 'N/A', 'releaseDate' => 'N/A', 'daysAgo' => 'N/A', 'sessions' => 0, 'users' => 0 ]);
        return;
    }

    $versionKeys = array_keys($sortedDatedVersionsMap);
    if (empty($versionKeys)) { 
        echo json_encode(['versionNumber' => 'Error', 'releaseDate' => 'Error', 'daysAgo' => 'Error', 'sessions' => 0, 'users' => 0 ]);
        return;
    }
    $latestVersionNumber = end($versionKeys);
    
    if (!isset($allVersionDetails[$latestVersionNumber])) {
         echo json_encode(['versionNumber' => 'Error: Version details not found', 'releaseDate' => 'Error', 'daysAgo' => 'Error', 'sessions' => 0, 'users' => 0 ]);
        return;
    }
    $latestVersionDetails = $allVersionDetails[$latestVersionNumber];
    $latestVersionParsedDate = $latestVersionDetails['parsed_release_date'];
    
    $today = new DateTime();
    $releaseDateObj = new DateTime($latestVersionParsedDate);
    $daysAgo = $today->diff($releaseDateObj)->days;

    $sessionsForLatest = 0;
    $uniqueClientsMapForLatest = [];
    $sortedDatedVersionNumbersOnly = array_keys($sortedDatedVersionsMap); 

    $stmtAnalytics = $pdo->query("SELECT client_id, version_number, launch_count FROM analytics");
    $analyticsData = $stmtAnalytics->fetchAll();

    foreach ($analyticsData as $row) {
        $originalVersion = $row['version_number'];
        $groupedVersionKey = getGroupedVersion($originalVersion, $allVersionDetails, $sortedDatedVersionNumbersOnly);

        if ($groupedVersionKey === $latestVersionNumber) {
            $sessionsForLatest += (int)$row['launch_count'];
            $uniqueClientsMapForLatest[$row['client_id']] = true;
        }
    }

    echo json_encode([
        'versionNumber' => $latestVersionNumber,
        'releaseDate' => $latestVersionDetails['original_release_date'], 
        'daysAgo' => $daysAgo,
        'sessions' => $sessionsForLatest,
        'users' => count($uniqueClientsMapForLatest)
    ]);
}

?>
