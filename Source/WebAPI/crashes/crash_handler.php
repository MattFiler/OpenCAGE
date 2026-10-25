<?php

if (file_exists('../keys.php')) {
    require_once '../keys.php';
} else {
    http_response_code(500);
    echo json_encode(['error' => 'Database keys file not found. Please create keys.php']);
    exit;
}

$conn = new mysqli("127.0.0.1", $username, $password, "OpenCAGE");
if ($conn->connect_error) {
    die("Connection failed: " . $conn->connect_error);
}

if ($_SERVER["REQUEST_METHOD"] == "POST") {
    $error_log = $conn->real_escape_string($_POST['error_log']);
    $application_version = $conn->real_escape_string($_POST['application_version']);
    $game_version = $conn->real_escape_string($_POST['game_version']);
    $datetime = $conn->real_escape_string($_POST['datetime']);
    $uptime = $conn->real_escape_string($_POST['uptime']);
    $os_name = $conn->real_escape_string($_POST['os_name']);
    $cpu_name = $conn->real_escape_string($_POST['cpu_name']);
    $ram_total = $conn->real_escape_string($_POST['ram_total']); 
    $current_level = $conn->real_escape_string($_POST['current_level']);
    $current_composite = $conn->real_escape_string($_POST['current_composite']);
    $current_entity = $conn->real_escape_string($_POST['current_entity']);

    // SQL query to insert data into the 'crashes' table
    $sql = "INSERT INTO crashes (error_log, application_version, game_version, datetime, uptime, os_name, cpu_name, ram_total, current_level, current_composite, current_entity)
            VALUES ('$error_log', '$application_version', '$game_version', '$datetime', '$uptime', '$os_name', '$cpu_name', '$ram_total', '$current_level', '$current_composite', '$current_entity')";

    if ($conn->query($sql) === TRUE) {
        http_response_code(200);
        echo "New record created successfully";
    } else {
        http_response_code(500);
        echo "Error: " . $sql . "<br>" . $conn->error;
    }
} else {
    http_response_code(405);
    echo "Method not allowed";
}

$conn->close();