<?php

use Aws\S3\S3Client;

class FmiCrexdataS3Client extends ApiClient
{
    private const string ENDPOINT_URL = "https://lake.fmi.fi";

    private readonly S3Client $client;

    public function __construct(
        private readonly AwsS3Credentials $aws_credentials
    ) {
        parent::__construct();

        $this->addDefaultRetryPolicy(ApiClient::DEFAULT_MAX_RETRIES);

        $this->client = new S3Client([
            "version"     => "latest",
            "region"      => $this->aws_credentials->region,
            "endpoint"    => self::ENDPOINT_URL,
            "credentials" => [
                "key"    => $this->aws_credentials->access_key_id,
                "secret" => $this->aws_credentials->secret_access_key,
            ]
        ]);
    }

    public function listAvailableFiles(DateTimeImmutable $date): array
    {
        $files = [];

        try {
            $result = $this->client->listObjectsV2([
                "Bucket" => $this->aws_credentials->bucket,
                "Prefix" => "forecasts/" . $date->format("Y-m-d"),
            ]);

            if (!isset($result["Contents"])) {
                Log::getInstance()->warning("No files found.");
                return [];
            }

            foreach ($result["Contents"] as $object) {
                if (!str_ends_with($object["Key"], ".csv")) {
                    continue;
                }

                $files[] = $object["Key"];
            }
        } catch (Aws\Exception\AwsException $e) {
            Log::getInstance()->severe("Error: " . $e->getMessage());
        }

        return $files;
    }

    public function downloadFiles(array $files, string $output_dir): void
    {
        try {
            foreach ($files as $file) {
                Log::getInstance()->debug("Downloading: $file");
                $save_path = $output_dir . "/" . basename($file);

                $this->client->getObject([
                    "Bucket" => $this->aws_credentials->bucket,
                    "Key"    => $file,
                    "SaveAs" => $save_path,
                ]);
            }
        } catch (Aws\Exception\AwsException $e) {
            Log::getInstance()->severe("Error: " . $e->getMessage());
        }
    }
}
