<?php
	include 'keys.php';
	header('Content-Type: text/plain');

	if(!isset($_GET["uid"]) || !isset($_GET["version"]) || !isset($_GET["staging"]) || ($_GET["staging"] != "yes" && $_GET["staging"] != "no")) 
	{
		echo "The required data was not sent.";
		exit();
	}
	$connection = mysqli_connect("localhost", $username, $password, "OpenCAGE");
	
	$result = mysqli_query($connection, "SELECT * FROM analytics WHERE client_id='".mysqli_real_escape_string($connection, $_GET["uid"])."' AND version_number='".mysqli_real_escape_string($connection, $_GET["version"])."' AND is_on_staging='".mysqli_real_escape_string($connection, $_GET["staging"])."'");
	
	if ($result->num_rows == 1) 
	{
		$obj = $result->fetch_object();
		if (!mysqli_query($connection, "UPDATE analytics SET launch_count='".($obj->launch_count + 1)."' WHERE client_id='".mysqli_real_escape_string($connection, $_GET["uid"])."' AND version_number='".mysqli_real_escape_string($connection, $_GET["version"])."' AND is_on_staging='".mysqli_real_escape_string($connection, $_GET["staging"])."'")) 
		{
			print_r(mysqli_error($connection));
		}
		else 
		{
			echo "SUCCESS_UPDATED_ENTRY";
		}
	}
	else if ($result->num_rows == 0) 
	{
		if (!mysqli_query($connection, "INSERT INTO analytics (client_id, version_number, is_on_staging, launch_count) VALUES ('".mysqli_real_escape_string($connection, $_GET["uid"])."','".mysqli_real_escape_string($connection, $_GET["version"])."','".mysqli_real_escape_string($connection, $_GET["staging"])."','1')")) 
		{
			print_r(mysqli_error($connection));
		}
		else 
		{
			echo "SUCCESS_NEW_ENTRY";
		}
	}
	else 
	{
		echo "LOGIC_ERROR";
	}
	
	mysqli_close($connection);
?>