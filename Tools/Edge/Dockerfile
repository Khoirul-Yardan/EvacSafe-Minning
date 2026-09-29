FROM python:3.12-slim
WORKDIR /app
RUN pip install --no-cache-dir paho-mqtt==2.1.0
COPY edge_service.py edge_routing.py /app/
CMD ["python", "-u", "/app/edge_service.py"]
