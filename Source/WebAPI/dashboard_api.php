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
         $already_added = false;
         foreach($tempVersions as $v) { if ($v['version_number'] === $row['version_number']) { $already_added = true; break; } }
         if (!$already_added) {
             $tempVersions[] = ['version_number' => $row['version_number'], 'original_release_date' => null, 'parsed_release_date' => null ];
         }
    }
    usort($tempVersions, function($a, $b) {
        $dateComparison = 0;
        if ($a['parsed_release_date'] && $b['parsed_release_date']) $dateComparison = strcmp($a['parsed_release_date'], $b['parsed_release_date']);
        elseif ($a['parsed_release_date']) $dateComparison = 1; 
        elseif ($b['parsed_release_date']) $dateComparison = -1; 
        return ($dateComparison === 0) ? version_compare($a['version_number'], $b['version_number']) : $dateComparison;
    });
    foreach($tempVersions as $vData) {
        $versions[$vData['version_number']] = ['original_release_date' => $vData['original_release_date'], 'parsed_release_date' => $vData['parsed_release_date']];
    }
    return $versions; 
}

// --- Helper Function: Get Sorted Dated Versions Map (Oldest to Newest) ---
function getSortedDatedVersionNumbersAndDates($allVersionDetails) {
    $sortableVersions = [];
    foreach ($allVersionDetails as $version => $details) {
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
    if (isset($allVersionDetails[$analyticsVersion]) && !empty($allVersionDetails[$analyticsVersion]['parsed_release_date'])) return $analyticsVersion;
    $groupedVersion = $analyticsVersion; 
    for ($i = count($sortedDatedVersionNumbersOnly) - 1; $i >= 0; $i--) {
        $datedVersionCandidate = $sortedDatedVersionNumbersOnly[$i];
        if (version_compare($datedVersionCandidate, $analyticsVersion, '<=')) {
            $groupedVersion = $datedVersionCandidate; break;
        }
    } return $groupedVersion;
}

// --- Helper Function: Calculate Linear Regression ---
function calculateLinearRegression($x_values, $y_values) {
    $n = count($x_values);
    if ($n <= 1 || $n !== count($y_values)) return [0, 0]; 

    $sum_x = array_sum($x_values); $sum_y = array_sum($y_values);
    $sum_xy = 0; $sum_x_sq = 0;

    for ($i = 0; $i < $n; $i++) { $sum_xy += $x_values[$i] * $y_values[$i]; $sum_x_sq += $x_values[$i] * $x_values[$i]; }

    $denominator = ($n * $sum_x_sq) - ($sum_x * $sum_x);
    if (abs($denominator) < 1e-9) return [0, $sum_y / $n]; 

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
        if ($clientId) getUserHistory($pdo, $clientId);
        else { http_response_code(400); echo json_encode(['error' => 'Client ID is required.']); }
        break;
    case 'getAllClientIds': getAllClientIds($pdo); break; 
    case 'getReleaseTimeline': getReleaseTimeline($pdo); break;
    case 'getPopularityData': getPopularityData($pdo); break;
    case 'getReleaseHeatmapData': getReleaseHeatmapData($pdo); break;
    case 'getTopUsers': getTopUsers($pdo); break;
    case 'getLatestVersionStats': getLatestVersionStats($pdo); break; 
    case 'getUsersOnLatestVersion': getUsersOnLatestVersion($pdo); break; // New Action
    default: http_response_code(404); echo json_encode(['error' => 'Action not found.']); break;
}

// --- API Functions ---
function getOverallStats($pdo) { 
    $totalUniqueUsers = $pdo->query("SELECT COUNT(DISTINCT client_id) FROM analytics")->fetchColumn();
    $totalSessions = $pdo->query("SELECT SUM(launch_count) FROM analytics")->fetchColumn();
    $totalVersions = $pdo->query("SELECT COUNT(id) FROM versions WHERE release_date IS NOT NULL AND TRIM(release_date) != ''")->fetchColumn();
    echo json_encode([
        'totalUniqueUsers' => (int)$totalUniqueUsers,
        'totalSessions' => (int)$totalSessions,
        'totalVersions' => (int)$totalVersions
    ]);
}
function getAllClientIds($pdo) { 
    $stmt = $pdo->query("
        SELECT client_id, SUM(launch_count) as total_sessions
        FROM analytics
        GROUP BY client_id
        ORDER BY total_sessions DESC
    ");
    $result = $stmt->fetchAll(PDO::FETCH_ASSOC); 
    echo json_encode($result);
}
function getStatsPerVersion($pdo) { 
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

        if (!isset($allVersionDetails[$groupedVersionKey])) continue; 

        if (!isset($groupedStats[$groupedVersionKey])) {
            $groupedStats[$groupedVersionKey] = ['version' => $groupedVersionKey, 'totalSessions' => 0, 'uniqueClientsMap' => []];
        }
        $groupedStats[$groupedVersionKey]['totalSessions'] += $launchCount;
        $groupedStats[$groupedVersionKey]['uniqueClientsMap'][$clientId] = true;
    }

    $resultArray = [];
    $totalSessionsSum = 0;
    $totalUniqueUsersSum = 0;
    $versionCount = 0;

    foreach ($groupedStats as $versionData) {
        $uniqueUsers = count($versionData['uniqueClientsMap']);
        $resultArray[] = ['version' => $versionData['version'], 'totalSessions' => $versionData['totalSessions'], 'uniqueUsers' => $uniqueUsers];
        $totalSessionsSum += $versionData['totalSessions'];
        $totalUniqueUsersSum += $uniqueUsers; 
        $versionCount++;
    }
    usort($resultArray, function($a, $b) { return version_compare($b['version'], $a['version']); });
    
    echo json_encode([
        'stats' => $resultArray,
        'averageSessions' => $versionCount > 0 ? round($totalSessionsSum / $versionCount, 2) : 0,
        'averageUniqueUsers' => $versionCount > 0 ? round((array_sum(array_column($resultArray, 'uniqueUsers'))) / $versionCount, 2) : 0
    ]);
}
function getUserHistory($pdo, $clientId) { 
    $allVersionDetails = getAllVersionDetails($pdo); 
    $stmt = $pdo->prepare("SELECT version_number, launch_count FROM analytics WHERE client_id = :client_id");
    $stmt->execute(['client_id' => $clientId]);
    $userAnalytics = $stmt->fetchAll();
    $userHistoryResult = [];
    foreach ($userAnalytics as $row) {
        $versionNumber = $row['version_number'];
        $sessions = (int)$row['launch_count'];
        $releaseDate = null;
        if (isset($allVersionDetails[$versionNumber]) && !empty($allVersionDetails[$versionNumber]['original_release_date'])) {
            $releaseDate = $allVersionDetails[$versionNumber]['original_release_date'];
        }
        $userHistoryResult[] = ['version' => $versionNumber, 'sessions' => $sessions, 'releaseDate' => $releaseDate ];
    }
    usort($userHistoryResult, function($a, $b) { return version_compare($b['version'], $a['version']); });
    echo json_encode($userHistoryResult);
}
function getReleaseTimeline($pdo) { 
    $allVersionDetails = getAllVersionDetails($pdo); 
    $timelineData = [];
    $sortedDatedVersionsMap = getSortedDatedVersionNumbersAndDates($allVersionDetails);
    $statsPerTimelineVersion = [];
    $stmtAnalytics = $pdo->query("SELECT client_id, version_number, launch_count FROM analytics");
    $analyticsData = $stmtAnalytics->fetchAll();
    $aggregatedStats = []; 
    $sortedDatedVersionNumbersOnly = array_keys($sortedDatedVersionsMap);
    foreach ($analyticsData as $row) {
        $originalVersion = $row['version_number']; $launchCount = (int)$row['launch_count']; $clientId = $row['client_id'];
        $groupedVersionKey = getGroupedVersion($originalVersion, $allVersionDetails, $sortedDatedVersionNumbersOnly);
        if (!isset($sortedDatedVersionsMap[$groupedVersionKey])) continue;
        if (!isset($aggregatedStats[$groupedVersionKey])) $aggregatedStats[$groupedVersionKey] = ['sessions' => 0, 'user_ids' => []];
        $aggregatedStats[$groupedVersionKey]['sessions'] += $launchCount;
        if (!in_array($clientId, $aggregatedStats[$groupedVersionKey]['user_ids'])) $aggregatedStats[$groupedVersionKey]['user_ids'][] = $clientId;
    }
    foreach ($aggregatedStats as $version => $data) $statsPerTimelineVersion[$version] = ['sessions' => $data['sessions'], 'users' => count($data['user_ids'])];
    $versionKeysSortedByDate = array_keys($sortedDatedVersionsMap); $today = new DateTime(); $calculatedDateMetrics = [];
    for ($i = 0; $i < count($versionKeysSortedByDate); $i++) {
        $currentVersion = $versionKeysSortedByDate[$i]; $currentReleaseDateStr = $sortedDatedVersionsMap[$currentVersion]; $currentReleaseDateObj = new DateTime($currentReleaseDateStr);
        $daysSincePrevious = null; if ($i > 0) { $prevVersion = $versionKeysSortedByDate[$i-1]; $prevReleaseDateStr = $sortedDatedVersionsMap[$prevVersion]; $prevReleaseDateObj = new DateTime($prevReleaseDateStr); $daysSincePrevious = $currentReleaseDateObj->diff($prevReleaseDateObj)->days; }
        $daysAvailableUntilNext = null; if ($i + 1 < count($versionKeysSortedByDate)) { $nextVersion = $versionKeysSortedByDate[$i+1]; $nextReleaseDateStr = $sortedDatedVersionsMap[$nextVersion]; $nextReleaseDateObj = new DateTime($nextReleaseDateStr); $daysAvailableUntilNext = $nextReleaseDateObj->diff($currentReleaseDateObj)->days; } else { $daysAvailableUntilNext = $today->diff($currentReleaseDateObj)->days; }
        $daysAvailableUntilNext = max(1, $daysAvailableUntilNext);
        $calculatedDateMetrics[$currentVersion] = ['daysSincePreviousRelease' => $daysSincePrevious, 'daysAvailable' => $daysAvailableUntilNext];
    }
    foreach ($sortedDatedVersionsMap as $version => $parsedDate) {
        $timelineData[] = [
            'version' => $version, 'releaseDate' => $parsedDate, 'originalReleaseDate' => $allVersionDetails[$version]['original_release_date'],
            'sessions' => $statsPerTimelineVersion[$version]['sessions'] ?? 0, 'users' => $statsPerTimelineVersion[$version]['users'] ?? 0,
            'daysAvailable' => $calculatedDateMetrics[$version]['daysAvailable'] ?? 0, 'daysSincePreviousRelease' => $calculatedDateMetrics[$version]['daysSincePreviousRelease'] 
        ];
    } echo json_encode($timelineData);
}
function getPopularityData($pdo) { 
    $allVersionDetails = getAllVersionDetails($pdo);
    $sortedDatedVersionsMap = getSortedDatedVersionNumbersAndDates($allVersionDetails); 
    $sortedDatedVersionNumbersOnly = array_keys($sortedDatedVersionsMap);
    $sessionsPerGroupedVersion = []; $uniqueUsersPerGroupedVersion = [];
    $stmtAnalytics = $pdo->query("SELECT client_id, version_number, launch_count FROM analytics");
    $analyticsData = $stmtAnalytics->fetchAll();
    foreach ($analyticsData as $row) {
        $originalVersion = $row['version_number']; $clientId = $row['client_id']; $launchCount = (int)$row['launch_count'];
        $groupedVersionKey = getGroupedVersion($originalVersion, $allVersionDetails, $sortedDatedVersionNumbersOnly);
        if (!isset($allVersionDetails[$groupedVersionKey])) continue;
        if (!isset($sessionsPerGroupedVersion[$groupedVersionKey])) $sessionsPerGroupedVersion[$groupedVersionKey] = 0;
        $sessionsPerGroupedVersion[$groupedVersionKey] += $launchCount;
        if (!isset($uniqueUsersPerGroupedVersion[$groupedVersionKey])) $uniqueUsersPerGroupedVersion[$groupedVersionKey] = [];
        $uniqueUsersPerGroupedVersion[$groupedVersionKey][$clientId] = true;
    }
    $popularityResult = []; $today = new DateTime(); $versionKeysSortedByDate = array_keys($sortedDatedVersionsMap);
    $x_values_for_trend = []; $y_values_for_trend = []; 
    for ($i = 0; $i < count($versionKeysSortedByDate); $i++) {
        $version = $versionKeysSortedByDate[$i]; $details = $allVersionDetails[$version]; $releaseDateObj = new DateTime($details['parsed_release_date']);
        $daysSincePreviousRelease = null; if ($i > 0) { $prevVersionReleaseDateObj = new DateTime($sortedDatedVersionsMap[$versionKeysSortedByDate[$i-1]]); $daysSincePreviousRelease = $releaseDateObj->diff($prevVersionReleaseDateObj)->days; }
        $daysAvailable = ($i + 1 < count($versionKeysSortedByDate)) ? (new DateTime($sortedDatedVersionsMap[$versionKeysSortedByDate[$i+1]]))->diff($releaseDateObj)->days : $today->diff($releaseDateObj)->days; 
        $daysAvailable = max(1, $daysAvailable);
        $uniqueUserCount = isset($uniqueUsersPerGroupedVersion[$version]) ? count($uniqueUsersPerGroupedVersion[$version]) : 0;
        $totalSessionsForVersion = $sessionsPerGroupedVersion[$version] ?? 0;
        $sessionsPerUser = ($uniqueUserCount > 0) ? ($totalSessionsForVersion / $uniqueUserCount) : 0;
        $engagementFactor = log10($sessionsPerUser + 1); 
        $daysFactor = log10($daysAvailable + 1); if ($daysFactor <= 0) $daysFactor = log10(2); 
        $basePopularity = ($uniqueUserCount > 0) ? ($uniqueUserCount / $daysFactor) : 0;
        $popularityScore = $basePopularity * (1 + $engagementFactor);
        $releaseTimestamp = $releaseDateObj->getTimestamp();
        $popularityResult[] = [
            'version' => $version, 'releaseDate' => $details['parsed_release_date'], 'originalReleaseDate' => $details['original_release_date'],
            'totalUsers' => $uniqueUserCount, 'totalSessions' => $totalSessionsForVersion, 'popularityScore' => round($popularityScore, 3), 
            'daysSincePreviousRelease' => $daysSincePreviousRelease, 'daysAvailable' => $daysAvailable, 'releaseTimestamp' => $releaseTimestamp 
        ];
        $x_values_for_trend[] = $releaseTimestamp; $y_values_for_trend[] = $popularityScore;
    }
    list($trendSlope, $trendIntercept) = calculateLinearRegression($x_values_for_trend, $y_values_for_trend);
    echo json_encode(['data' => $popularityResult, 'trendline' => ['slope' => $trendSlope, 'intercept' => $trendIntercept]]);
}
function getReleaseHeatmapData($pdo) { 
    $allVersionDetails = getAllVersionDetails($pdo);
    if (empty($allVersionDetails)) { echo json_encode([]); return; }
    $releaseDatesOnly = []; foreach($allVersionDetails as $details) $releaseDatesOnly[] = $details['parsed_release_date']; sort($releaseDatesOnly); 
    if (empty($releaseDatesOnly)) { echo json_encode([]); return; }
    $firstReleaseDate = new DateTime(reset($releaseDatesOnly)); $heatmapEndDate = new DateTime(max(end($releaseDatesOnly), (new DateTime('now'))->format('Y-m-d'))); $heatmapEndDate->modify('last day of this month');
    $heatmapData = []; $period = new DatePeriod($firstReleaseDate, new DateInterval('P1M'), $heatmapEndDate->modify('+1 day')); $releasesByMonth = [];
    foreach ($releaseDatesOnly as $dateStr) { $monthYear = (new DateTime($dateStr))->format('Y-m'); $releasesByMonth[$monthYear] = ($releasesByMonth[$monthYear] ?? 0) + 1; }
    $maxReleasesInMonth = !empty($releasesByMonth) ? max($releasesByMonth) : 1; if ($maxReleasesInMonth == 0) $maxReleasesInMonth = 1; 
    foreach ($period as $dt) {
        $monthYearKey = $dt->format('Y-m'); $releaseCount = $releasesByMonth[$monthYearKey] ?? 0;
        $heatmapData[] = ['month' => $dt->format('M'), 'year' => $dt->format('Y'), 'isoMonthYear' => $monthYearKey, 'releaseCount' => $releaseCount, 'intensity' => $releaseCount / $maxReleasesInMonth];
    } echo json_encode($heatmapData);
}
function getTopUsers($pdo) { 
    $allVersionDetails = getAllVersionDetails($pdo); $sortedDatedVersionsMap = getSortedDatedVersionNumbersAndDates($allVersionDetails); $sortedDatedVersionNumbersOnly = array_keys($sortedDatedVersionsMap);
    $stmtTopUsers = $pdo->query("SELECT client_id, SUM(launch_count) as total_sessions FROM analytics GROUP BY client_id ORDER BY total_sessions DESC LIMIT 10");
    $topUsersData = $stmtTopUsers->fetchAll(); $rankedUsers = []; $rank = 1;
    foreach ($topUsersData as $userData) {
        $clientId = $userData['client_id'];
        $stmtClientVersions = $pdo->prepare("SELECT DISTINCT version_number FROM analytics WHERE client_id = :client_id");
        $stmtClientVersions->execute(['client_id' => $clientId]); $rawVersionsUsed = $stmtClientVersions->fetchAll(PDO::FETCH_COLUMN);
        $groupedVersionsUsedMap = [];
        foreach($rawVersionsUsed as $rawVersion) { $groupedVersion = getGroupedVersion($rawVersion, $allVersionDetails, $sortedDatedVersionNumbersOnly); if (isset($allVersionDetails[$groupedVersion])) $groupedVersionsUsedMap[$groupedVersion] = true; }
        $groupedVersionsList = array_keys($groupedVersionsUsedMap); usort($groupedVersionsList, 'version_compare'); 
        $rankedUsers[] = ['rank' => $rank++, 'clientId' => $clientId, 'totalSessions' => (int)$userData['total_sessions'], 'versionsUsedCount' => count($groupedVersionsList), 'versionsUsedList' => $groupedVersionsList ];
    } echo json_encode($rankedUsers);
}
function getLatestVersionStats($pdo) { 
    $allVersionDetails = getAllVersionDetails($pdo); $sortedDatedVersionsMap = getSortedDatedVersionNumbersAndDates($allVersionDetails);
    if (empty($sortedDatedVersionsMap)) { echo json_encode(['versionNumber' => 'N/A', 'releaseDate' => 'N/A', 'daysAgo' => 'N/A', 'sessions' => 0, 'users' => 0 ]); return; }
    $versionKeys = array_keys($sortedDatedVersionsMap); if (empty($versionKeys)) { echo json_encode(['versionNumber' => 'Error', 'releaseDate' => 'Error', 'daysAgo' => 'Error', 'sessions' => 0, 'users' => 0 ]); return; }
    $latestVersionNumber = end($versionKeys); if (!isset($allVersionDetails[$latestVersionNumber])) { echo json_encode(['versionNumber' => 'Error: Version details not found', 'releaseDate' => 'Error', 'daysAgo' => 'Error', 'sessions' => 0, 'users' => 0 ]); return; }
    $latestVersionDetails = $allVersionDetails[$latestVersionNumber]; $latestVersionParsedDate = $latestVersionDetails['parsed_release_date'];
    $today = new DateTime(); $releaseDateObj = new DateTime($latestVersionParsedDate); $daysAgo = $today->diff($releaseDateObj)->days;
    $sessionsForLatest = 0; $uniqueClientsMapForLatest = []; $sortedDatedVersionNumbersOnly = array_keys($sortedDatedVersionsMap); 
    $stmtAnalytics = $pdo->query("SELECT client_id, version_number, launch_count FROM analytics"); $analyticsData = $stmtAnalytics->fetchAll();
    foreach ($analyticsData as $row) {
        $originalVersion = $row['version_number']; $groupedVersionKey = getGroupedVersion($originalVersion, $allVersionDetails, $sortedDatedVersionNumbersOnly);
        if ($groupedVersionKey === $latestVersionNumber) { $sessionsForLatest += (int)$row['launch_count']; $uniqueClientsMapForLatest[$row['client_id']] = true; }
    }
    echo json_encode(['versionNumber' => $latestVersionNumber, 'releaseDate' => $latestVersionDetails['original_release_date'], 'daysAgo' => $daysAgo, 'sessions' => $sessionsForLatest, 'users' => count($uniqueClientsMapForLatest)]);
}

// --- New API Function: getUsersOnLatestVersion ---
function getUsersOnLatestVersion($pdo) {
    // Find the latest version with a release date
    $stmtLatest = $pdo->query("SELECT version_number FROM versions 
                               WHERE release_date IS NOT NULL AND TRIM(release_date) != '' 
                               ORDER BY STR_TO_DATE(release_date, '%D %M %Y') DESC, version_number DESC 
                               LIMIT 1");
    $latestVersionInfo = $stmtLatest->fetch();

    if (!$latestVersionInfo) {
        echo json_encode([]); // Return empty array if no latest version found
        return;
    }
    $latestVersionNumber = $latestVersionInfo['version_number'];

    // Fetch users and their session counts ONLY for the latest version
    $stmtUsers = $pdo->prepare("
        SELECT client_id, SUM(launch_count) as sessions_on_latest
        FROM analytics
        WHERE version_number = :latest_version 
        GROUP BY client_id
        ORDER BY sessions_on_latest DESC
    ");
    $stmtUsers->execute(['latest_version' => $latestVersionNumber]);
    $usersData = $stmtUsers->fetchAll();

    // Format the result
    $result = [];
    foreach ($usersData as $row) {
        $result[] = [
            'clientId' => $row['client_id'],
            'sessionsOnLatest' => (int)$row['sessions_on_latest']
        ];
    }

    echo json_encode($result);
}

?>